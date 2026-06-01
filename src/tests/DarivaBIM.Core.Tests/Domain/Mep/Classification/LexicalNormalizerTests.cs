using System.Collections.Generic;
using DarivaBIM.Domain.Mep.Classification.Lexical;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Tests do LexicalNormalizer (secao 10.1). Codigo PROPRIO do Mep, sem dependencia
/// de Tigre (D5) — os golden tests provam normalizacao equivalente a TigreTextUtils
/// usando os mesmos exemplos, mas sem tocar/referenciar Tigre.
/// </summary>
public class LexicalNormalizerTests
{
    private static readonly TokenizerOptions Default = new();

    // Sem expansao de alias: isola split/boundary/negative/acento dos sinonimos.
    private static readonly TokenizerOptions NoAlias = new() { ExpandAliases = false };

    private static IReadOnlyList<string> Tok(string raw, TokenizerOptions? opts = null)
        => LexicalNormalizer.Tokenize(raw, opts ?? Default);

    [Fact]
    public void Boundary_te_does_not_match_inside_terminal()
    {
        var tokens = Tok("registro terminal", NoAlias);

        Assert.DoesNotContain("te", tokens);
        Assert.Contains("terminal", tokens);
        Assert.Contains("registro", tokens);
    }

    [Fact]
    public void Boundary_te_matches_as_isolated_token()
    {
        Assert.Contains("te", Tok("te 50", NoAlias));
    }

    [Fact]
    public void Negative_terminal_suppresses_te()
    {
        // "te" aparece como token isolado, mas "terminal" no contexto o suprime.
        var tokens = Tok("te terminal", NoAlias);

        Assert.DoesNotContain("te", tokens);
        Assert.Contains("terminal", tokens);
    }

    [Fact]
    public void Negative_snake_suppresses_sn()
    {
        Assert.DoesNotContain("sn", Tok("sn snake", NoAlias));
    }

    [Fact]
    public void Negative_sra_suppresses_sr()
    {
        Assert.DoesNotContain("sr", Tok("sr sra", NoAlias));
    }

    [Fact]
    public void Accent_is_stripped()
    {
        Assert.Equal(new[] { "soldavel" }, Tok("Soldável", NoAlias));
        Assert.Equal(new[] { "reducao" }, Tok("Redução", NoAlias));
    }

    [Fact]
    public void CamelCase_splits_acronym_from_word()
    {
        Assert.Equal(new[] { "esg", "redux" }, Tok("ESGRedux", NoAlias));
    }

    [Fact]
    public void CamelCase_keeps_pure_acronyms_intact()
    {
        // PPR/PN/SR/SN sao siglas: sem [a-z] na sequencia, nao sao quebradas.
        Assert.Equal(new[] { "ppr" }, Tok("PPR", NoAlias));
        Assert.Equal(new[] { "pn" }, Tok("PN", NoAlias));
        Assert.Equal(new[] { "sr" }, Tok("SR", NoAlias));
        Assert.Equal(new[] { "sn" }, Tok("SN", NoAlias));
    }

    [Fact]
    public void Split_breaks_on_separators()
    {
        Assert.Equal(
            new[] { "esg", "redux", "joelho", "45", "90" },
            Tok("ESG_Redux_Joelho 45_90", NoAlias));
    }

    [Fact]
    public void Split_breaks_dimension_x_between_digits_but_not_inside_words()
    {
        // x entre digitos separa ("25x50"); x dentro de palavra NAO ("Redux").
        Assert.Equal(new[] { "25", "50" }, Tok("25x50", NoAlias));
        Assert.Contains("redux", Tok("Redux", NoAlias));
        Assert.DoesNotContain("redu", Tok("Redux", NoAlias));
    }

    [Fact]
    public void Alias_expands_family_bidirectionally()
    {
        // Contrato definido: a chave traz as variantes e qualquer variante traz a
        // familia inteira (texto com "elbow" passa a casar o hint "joelho").
        var fromKey = Tok("joelho");
        Assert.Contains("joelho", fromKey);
        Assert.Contains("elbow", fromKey);
        Assert.Contains("cotovelo", fromKey);

        var fromVariant = Tok("elbow");
        Assert.Contains("joelho", fromVariant);
        Assert.Contains("elbow", fromVariant);
        Assert.Contains("cotovelo", fromVariant);
    }

    [Fact]
    public void Alias_off_does_not_expand()
    {
        Assert.Equal(new[] { "joelho" }, Tok("joelho", NoAlias));
    }

    [Fact]
    public void Result_is_a_set_deduplicated_in_first_appearance_order()
    {
        Assert.Equal(new[] { "te", "50" }, Tok("te te 50", NoAlias));
    }

    [Theory]
    [InlineData("Soldável", "soldavel")]
    [InlineData("Roscável", "roscavel")]
    [InlineData("Redução", "reducao")]
    [InlineData("União", "uniao")]
    [InlineData("Tê", "te")]
    [InlineData("Junção", "juncao")]
    public void Golden_accent_normalization(string raw, string expected)
    {
        // Golden (D5): mesmos exemplos de acento/case que o TigreTextUtils trataria,
        // provando normalizacao equivalente com ZERO dependencia de Tigre.
        Assert.Equal(new[] { expected }, Tok(raw, NoAlias));
    }

    [Theory]
    [InlineData("DN50x25", new[] { "dn50", "25" })]
    [InlineData("100x80x50", new[] { "100", "80", "50" })]
    public void Golden_dimension_split(string raw, string[] expected)
    {
        Assert.Equal(expected, Tok(raw, NoAlias));
    }
}
