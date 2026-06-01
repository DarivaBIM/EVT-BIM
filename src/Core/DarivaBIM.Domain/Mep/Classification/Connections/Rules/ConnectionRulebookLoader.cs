using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarivaBIM.Domain.Mep.Classification.Ports;

namespace DarivaBIM.Domain.Mep.Classification.Connections.Rules
{
    /// <summary>
    /// Carrega e valida um <see cref="ConnectionRulebookDocument"/> de JSON (decisao
    /// D3: System.Text.Json + EmbeddedResource, espelhando TigreFallbackCatalogRows).
    /// Resolve a heranca (inherits/overrides via deep-merge) e expande o shortcut
    /// string de diameterRule; valida IDs unicos, referencias (inherits/promoteTo
    /// apontam para Id existente) e ausencia de ciclo de inherits.
    /// </summary>
    public static class ConnectionRulebookLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new DiameterRuleJsonConverter());
            return options;
        }

        /// <summary>Carrega de uma string JSON (testavel com fixture).</summary>
        public static ConnectionRulebookDocument Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Rulebook JSON vazio.");
            }

            return Finalize(JsonSerializer.Deserialize<ConnectionRulebookDocument>(json, JsonOptions));
        }

        /// <summary>Carrega de um Stream (testavel / reuso interno).</summary>
        public static ConnectionRulebookDocument Load(Stream stream)
        {
            return Finalize(JsonSerializer.Deserialize<ConnectionRulebookDocument>(stream, JsonOptions));
        }

        /// <summary>Carrega de um EmbeddedResource (producao). Espelha TigreFallbackCatalogRows.</summary>
        public static ConnectionRulebookDocument LoadEmbedded(Assembly assembly, string logicalName)
        {
            using Stream? stream = assembly.GetManifestResourceStream(logicalName);
            if (stream is null)
            {
                throw new InvalidOperationException(
                    $"Embedded resource '{logicalName}' nao encontrado em {assembly.FullName}.");
            }

            return Load(stream);
        }

        private static ConnectionRulebookDocument Finalize(ConnectionRulebookDocument? doc)
        {
            if (doc is null)
            {
                throw new InvalidOperationException("Rulebook JSON desserializou para null.");
            }

            IReadOnlyDictionary<string, ConnectionRule> byId = BuildIndex(doc.Rules);
            ValidateReferences(doc.Rules, byId);

            // Resolve a topologia de cada regra (deep-merge de inherits). cache evita
            // re-resolver cadeias compartilhadas; resolving detecta ciclos.
            var cache = new Dictionary<string, TopologyConstraint>(StringComparer.Ordinal);
            var resolvedRules = new List<ConnectionRule>(doc.Rules.Count);
            foreach (ConnectionRule rule in doc.Rules)
            {
                TopologyConstraint topology = ResolveTopology(
                    rule, byId, cache, new HashSet<string>(StringComparer.Ordinal));
                resolvedRules.Add(rule with { Topology = topology });
            }

            return doc with { Rules = resolvedRules };
        }

        private static IReadOnlyDictionary<string, ConnectionRule> BuildIndex(IReadOnlyList<ConnectionRule> rules)
        {
            // netstandard2.0 nao tem Dictionary.TryAdd -> ContainsKey + Add.
            var byId = new Dictionary<string, ConnectionRule>(StringComparer.Ordinal);
            foreach (ConnectionRule rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Id))
                {
                    throw new InvalidOperationException("Regra com Id vazio no rulebook.");
                }

                if (byId.ContainsKey(rule.Id))
                {
                    throw new InvalidOperationException($"Id de regra duplicado: '{rule.Id}'.");
                }

                byId.Add(rule.Id, rule);
            }

            return byId;
        }

        private static void ValidateReferences(
            IReadOnlyList<ConnectionRule> rules, IReadOnlyDictionary<string, ConnectionRule> byId)
        {
            foreach (ConnectionRule rule in rules)
            {
                string? inherits = rule.Topology.Inherits;
                if (!string.IsNullOrWhiteSpace(inherits) && !byId.ContainsKey(inherits!))
                {
                    throw new InvalidOperationException(
                        $"Regra '{rule.Id}' herda de '{inherits}', que nao existe.");
                }

                foreach (LexicalDisambiguator disambiguator in rule.LexicalDisambiguators)
                {
                    if (!string.IsNullOrWhiteSpace(disambiguator.PromoteTo)
                        && !byId.ContainsKey(disambiguator.PromoteTo))
                    {
                        throw new InvalidOperationException(
                            $"Regra '{rule.Id}' promove para '{disambiguator.PromoteTo}', que nao existe.");
                    }
                }
            }
        }

        private static TopologyConstraint ResolveTopology(
            ConnectionRule rule,
            IReadOnlyDictionary<string, ConnectionRule> byId,
            Dictionary<string, TopologyConstraint> cache,
            HashSet<string> resolving)
        {
            if (cache.TryGetValue(rule.Id, out TopologyConstraint? cached))
            {
                return cached;
            }

            TopologyConstraint topology = rule.Topology;
            string? inherits = topology.Inherits;

            TopologyConstraint result;
            if (string.IsNullOrWhiteSpace(inherits))
            {
                // Base: sem heranca. Limpa Inherits/Overrides (estado ja resolvido).
                result = topology with { Inherits = null, Overrides = null };
            }
            else
            {
                // Add devolve false se o Id ja estava na pilha de resolucao = ciclo.
                if (!resolving.Add(rule.Id))
                {
                    throw new InvalidOperationException(
                        $"Ciclo de inherits detectado envolvendo '{rule.Id}'.");
                }

                ConnectionRule parent = byId[inherits!];
                TopologyConstraint parentTopology = ResolveTopology(parent, byId, cache, resolving);
                result = Merge(parentTopology, topology.Overrides);
                resolving.Remove(rule.Id);
            }

            cache[rule.Id] = result;
            return result;
        }

        // Deep-merge: parent ja resolvido + overrides do filho por cima. Campo nao
        // especificado no override (null / lista vazia) herda do pai (secao 12.1).
        private static TopologyConstraint Merge(TopologyConstraint parent, TopologyConstraint? overrides)
        {
            if (overrides is null)
            {
                return parent with { Inherits = null, Overrides = null };
            }

            return new TopologyConstraint
            {
                PartTypeAccepts = overrides.PartTypeAccepts.Count > 0
                    ? overrides.PartTypeAccepts
                    : parent.PartTypeAccepts,
                ConnectorCount = overrides.ConnectorCount ?? parent.ConnectorCount,
                DiameterRule = overrides.DiameterRule ?? parent.DiameterRule,
                PrimaryAngleRule = overrides.PrimaryAngleRule ?? parent.PrimaryAngleRule,
                LateralAngleRule = overrides.LateralAngleRule ?? parent.LateralAngleRule,
                Inherits = null,
                Overrides = null,
            };
        }
    }

    /// <summary>
    /// Le <see cref="DiameterRule"/> aceitando o SHORTCUT string (secao 12.2): uma
    /// string como "equal" expande para a forma canonica (um constraint com Ports
    /// vazio e a relacao informada); um objeto e parseado normalmente. Parse manual
    /// via JsonDocument no caso objeto para evitar recursao no proprio converter.
    /// </summary>
    internal sealed class DiameterRuleJsonConverter : JsonConverter<DiameterRule>
    {
        public override DiameterRule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new DiameterRule
                {
                    Mode = "roles",
                    Constraints = new[] { new DiameterConstraint { Relation = ParseRelation(reader.GetString()) } },
                };
            }

            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;

            string mode = root.TryGetProperty("mode", out JsonElement modeElement)
                && modeElement.ValueKind == JsonValueKind.String
                    ? modeElement.GetString() ?? "roles"
                    : "roles";

            var constraints = new List<DiameterConstraint>();
            if (root.TryGetProperty("constraints", out JsonElement constraintsElement)
                && constraintsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in constraintsElement.EnumerateArray())
                {
                    constraints.Add(ParseConstraint(element));
                }
            }

            return new DiameterRule { Mode = mode, Constraints = constraints };
        }

        public override void Write(Utf8JsonWriter writer, DiameterRule value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("mode", value.Mode);
            writer.WritePropertyName("constraints");
            JsonSerializer.Serialize(writer, value.Constraints, options);
            writer.WriteEndObject();
        }

        private static DiameterConstraint ParseConstraint(JsonElement element)
        {
            var ports = new List<PortRole>();
            if (element.TryGetProperty("ports", out JsonElement portsElement)
                && portsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement portElement in portsElement.EnumerateArray())
                {
                    if (portElement.ValueKind == JsonValueKind.String
                        && Enum.TryParse(portElement.GetString(), ignoreCase: true, out PortRole role))
                    {
                        ports.Add(role);
                    }
                }
            }

            DiameterRelation relation = DiameterRelation.Unknown;
            if (element.TryGetProperty("relation", out JsonElement relationElement)
                && relationElement.ValueKind == JsonValueKind.String)
            {
                relation = ParseRelation(relationElement.GetString());
            }

            string? target = element.TryGetProperty("target", out JsonElement targetElement)
                && targetElement.ValueKind == JsonValueKind.String
                    ? targetElement.GetString()
                    : null;

            return new DiameterConstraint { Ports = ports, Relation = relation, Target = target };
        }

        private static DiameterRelation ParseRelation(string? raw)
            => Enum.TryParse(raw, ignoreCase: true, out DiameterRelation relation)
                ? relation
                : DiameterRelation.Unknown;
    }
}
