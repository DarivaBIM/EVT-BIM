using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Revit.Adapters.V2025.Common.Parameters;

namespace DarivaBIM.Revit.Adapters.V2025.Features.TigreCodes
{
    /// <summary>
    /// Dados extraídos de um tubo para casamento contra o catálogo Tigre.
    /// </summary>
    internal readonly struct TigrePipeData
    {
        public TigrePipeData(string description, string segment, string typeName, int? diameterMm)
        {
            Description = description;
            Segment = segment;
            TypeName = typeName;
            DiameterMm = diameterMm;
        }

        public string Description { get; }
        public string Segment { get; }
        public string TypeName { get; }
        public int? DiameterMm { get; }
    }

    /// <summary>
    /// Lê os campos do tubo necessários para casar com o catálogo Tigre:
    /// descrição (com fallback de busca em vários nomes/parâmetros),
    /// segmento (<c>RBS_PIPE_SEGMENT_PARAM</c>), nome do <see cref="PipeType"/>
    /// e diâmetro nominal em milímetros. Reproduz o cascading do script
    /// Dynamo original.
    /// </summary>
    internal static class TigrePipeDataReader
    {
        private static readonly string[] DescriptionParamNames =
            { "Descrição", "Description", "Descriçao", "Descricao" };

        public static TigrePipeData Read(Document doc, Pipe pipe)
        {
            string description = GetPipeDescriptionText(doc, pipe);
            string segment = ParameterTextReader.Read(doc, pipe.get_Parameter(BuiltInParameter.RBS_PIPE_SEGMENT_PARAM));
            string typeName = GetPipeTypeName(doc, pipe);
            int? diameterMm = GetPipeDiameterMm(pipe);

            return new TigrePipeData(description, segment, typeName, diameterMm);
        }

        private static string GetPipeDescriptionText(Document doc, Pipe pipe)
        {
            string txt = GetParamTextByName(doc, pipe, DescriptionParamNames);
            if (!string.IsNullOrWhiteSpace(txt))
                return txt;

            txt = ParameterTextReader.Read(doc, pipe.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION));
            if (!string.IsNullOrWhiteSpace(txt))
                return txt;

            Element? pipeType = doc.GetElement(pipe.GetTypeId());
            txt = GetParamTextByName(doc, pipeType, DescriptionParamNames);
            if (!string.IsNullOrWhiteSpace(txt))
                return txt;

            txt = ParameterTextReader.Read(doc, pipeType?.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION));
            if (!string.IsNullOrWhiteSpace(txt))
                return txt;

            return pipeType?.Name ?? string.Empty;
        }

        private static string GetParamTextByName(Document doc, Element? element, IEnumerable<string> names)
        {
            if (element == null)
                return string.Empty;

            foreach (string name in names)
            {
                try
                {
                    string txt = ParameterTextReader.Read(doc, element.LookupParameter(name));
                    if (!string.IsNullOrWhiteSpace(txt))
                        return txt;
                }
                catch
                {
                    // continua
                }
            }

            return string.Empty;
        }

        private static string GetPipeTypeName(Document doc, Pipe pipe)
        {
            ElementId typeId = pipe.GetTypeId();
            if (typeId == null || typeId.Value <= 0)
                return string.Empty;

            Element? pipeType = doc.GetElement(typeId);
            return pipeType?.Name ?? string.Empty;
        }

        private static int? GetPipeDiameterMm(Pipe pipe)
        {
            Parameter? p = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            if (p == null)
                return null;

            try
            {
                double feet = p.AsDouble();
                double mm = UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
                return (int)Math.Round(mm);
            }
            catch
            {
                return null;
            }
        }
    }
}
