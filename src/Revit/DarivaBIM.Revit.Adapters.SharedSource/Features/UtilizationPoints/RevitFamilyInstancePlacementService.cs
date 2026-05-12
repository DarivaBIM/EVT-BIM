using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace DarivaBIM.Revit.Adapters.Features.UtilizationPoints
{
    /// <summary>
    /// Cria, posiciona, orienta e tenta conectar uma <see cref="FamilyInstance"/>
    /// no <see cref="Connector"/> de destino, replicando passo a passo o
    /// algoritmo do script Python de referência:
    /// 1) cria a instância no ponto do conector alvo;
    /// 2) move-a até que o conector livre da família coincida com o conector
    ///    alvo;
    /// 3) rotaciona a instância para que a direção do conector da família seja
    ///    oposta à do conector alvo;
    /// 4) reajusta a posição e chama <c>ConnectTo</c>.
    /// </summary>
    internal static class RevitFamilyInstancePlacementService
    {
        public enum PlacementOutcome
        {
            CreatedAndConnected,
            CreatedNotConnected,
            CreationFailed,
            NoFreeConnectorInFamily,
        }

        public sealed class PlacementResult
        {
            public PlacementResult(PlacementOutcome outcome, FamilyInstance? instance, string message)
            {
                Outcome = outcome;
                Instance = instance;
                Message = message ?? string.Empty;
            }

            public PlacementOutcome Outcome { get; }
            public FamilyInstance? Instance { get; }
            public string Message { get; }
        }

        public static PlacementResult Place(
            Document doc,
            FamilySymbol symbol,
            Connector targetConnector,
            Element referenceElement,
            Level? referenceLevel,
            bool autoConnect)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            if (targetConnector == null) throw new ArgumentNullException(nameof(targetConnector));

            ActivateSymbol(doc, symbol);

            XYZ targetPoint = targetConnector.Origin;
            XYZ? targetDirection = RevitConnectorUtilities.GetDirection(targetConnector);
            if (targetDirection == null)
            {
                return new PlacementResult(
                    PlacementOutcome.CreationFailed,
                    null,
                    "Não foi possível obter a direção do conector alvo.");
            }

            FamilyInstance? instance = TryCreateInstance(doc, symbol, targetPoint, referenceLevel, referenceElement);
            if (instance == null)
            {
                return new PlacementResult(
                    PlacementOutcome.CreationFailed,
                    null,
                    $"Não foi possível criar a instância da família '{symbol.Family?.Name} : {symbol.Name}'.");
            }

            doc.Regenerate();

            Connector? familyConnector = MoveFamilyConnectorToPoint(doc, instance, targetPoint);
            if (familyConnector == null)
            {
                return new PlacementResult(
                    PlacementOutcome.NoFreeConnectorInFamily,
                    instance,
                    "Família criada, mas nenhum conector livre foi encontrado nela.");
            }

            XYZ desiredDirection = targetDirection.Negate();
            RotateConnectorToDirection(doc, instance, familyConnector, desiredDirection);

            doc.Regenerate();

            // Segunda rodada de ajuste após o rotate (igual ao script Python).
            familyConnector = MoveFamilyConnectorToPoint(doc, instance, targetPoint);
            doc.Regenerate();

            familyConnector = RevitConnectorUtilities.GetClosestConnector(instance, targetPoint, onlyFree: true);
            if (familyConnector == null)
            {
                return new PlacementResult(
                    PlacementOutcome.NoFreeConnectorInFamily,
                    instance,
                    "Família criada, mas não foi possível recuperar o conector dela.");
            }

            if (!autoConnect)
            {
                return new PlacementResult(
                    PlacementOutcome.CreatedNotConnected,
                    instance,
                    "Família criada (conexão automática desativada).");
            }

            try
            {
                familyConnector.ConnectTo(targetConnector);
                return new PlacementResult(
                    PlacementOutcome.CreatedAndConnected,
                    instance,
                    "Família criada e conectada.");
            }
            catch (Exception ex)
            {
                return new PlacementResult(
                    PlacementOutcome.CreatedNotConnected,
                    instance,
                    $"Família criada, mas não conectou: {ex.Message}");
            }
        }

        private static void ActivateSymbol(Document doc, FamilySymbol symbol)
        {
            try
            {
                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }
            }
            catch
            {
                // Symbol não ativável — segue; a próxima chamada quebrará e
                // será reportada como CreationFailed.
            }
        }

        private static FamilyInstance? TryCreateInstance(
            Document doc,
            FamilySymbol symbol,
            XYZ point,
            Level? userLevel,
            Element referenceElement)
        {
            if (userLevel != null)
            {
                try
                {
                    return doc.Create.NewFamilyInstance(point, symbol, userLevel, StructuralType.NonStructural);
                }
                catch { /* fallback */ }
            }

            try
            {
                return doc.Create.NewFamilyInstance(point, symbol, StructuralType.NonStructural);
            }
            catch { /* fallback */ }

            Level? elementLevel = RevitReferenceLevelResolver.GetElementLevel(doc, referenceElement);
            if (elementLevel != null)
            {
                try
                {
                    return doc.Create.NewFamilyInstance(point, symbol, elementLevel, StructuralType.NonStructural);
                }
                catch { /* nada mais a tentar */ }
            }

            return null;
        }

        private static Connector? MoveFamilyConnectorToPoint(Document doc, FamilyInstance instance, XYZ targetPoint)
        {
            Connector? familyConnector = RevitConnectorUtilities.GetClosestConnector(instance, targetPoint, onlyFree: true);
            if (familyConnector == null) return null;

            try
            {
                XYZ delta = targetPoint.Subtract(familyConnector.Origin);
                if (delta.GetLength() > 0.0001)
                {
                    ElementTransformUtils.MoveElement(doc, instance.Id, delta);
                    doc.Regenerate();
                }
            }
            catch
            {
                // ignora — o ConnectTo tentará novamente após reposicionar
            }

            return RevitConnectorUtilities.GetClosestConnector(instance, targetPoint, onlyFree: true);
        }

        private static void RotateConnectorToDirection(
            Document doc,
            FamilyInstance instance,
            Connector familyConnector,
            XYZ desiredDirection)
        {
            XYZ? currentDirection = RevitConnectorUtilities.GetDirection(familyConnector);
            if (currentDirection == null || desiredDirection == null) return;

            double dot = currentDirection.DotProduct(desiredDirection);
            if (dot > 1.0) dot = 1.0;
            if (dot < -1.0) dot = -1.0;

            double angle = Math.Acos(dot);
            if (Math.Abs(angle) < 1e-6) return;

            XYZ axis = currentDirection.CrossProduct(desiredDirection);
            if (axis.GetLength() < 1e-6)
            {
                axis = currentDirection.CrossProduct(XYZ.BasisX);
                if (axis.GetLength() < 1e-6)
                    axis = currentDirection.CrossProduct(XYZ.BasisY);
            }

            if (axis.GetLength() < 1e-6) return;

            try
            {
                axis = axis.Normalize();
                Line axisLine = Line.CreateUnbound(familyConnector.Origin, axis);
                ElementTransformUtils.RotateElement(doc, instance.Id, axisLine, angle);
            }
            catch
            {
                // Rotação opcional — falha não invalida a inserção.
            }
        }
    }
}
