namespace OpcPlc.PluginNodes
{
    using Opc.Ua;
    using OpcPlc.PluginNodes.Models;
    using System;
    using System.Collections.Generic;
    using static OpcPlc.Program;

    /// <summary>
    /// Predefined nodes to simulate a machine at M&F Engineering: Temperature, Product Counter, Speed.
    /// </summary>
    public class M8FMachineNodes : IPluginNodes
    {
        public IReadOnlyCollection<NodeWithIntervals> Nodes { get; private set; } = new List<NodeWithIntervals>();

        private static bool _isEnabled = true;
        private PlcNodeManager _plcNodeManager;
        private SimulatedVariableNode<uint> _productCounterNode;
        private SimulatedVariableNode<double> _temperatureNode;
        private readonly Random _random = new Random();
        private int _productCounterCycleInPhase;
        private int _temperatureCycleInPhase;

        public void AddOptions(Mono.Options.OptionSet optionSet)
        {
            optionSet.Add(
                "m8fn|m8fmachinenodes",
                $"add nodes to simulate machine at M&F Engineering.\nDefault: {_isEnabled}",
                (string s) => _isEnabled = s != null);
        }

        public void AddToAddressSpace(FolderState telemetryFolder, FolderState methodsFolder, PlcNodeManager plcNodeManager)
        {
            _plcNodeManager = plcNodeManager;

            if (_isEnabled)
            {
                FolderState folder = _plcNodeManager.CreateFolder(
                    telemetryFolder,
                    path: "M8F",
                    name: "M8F",
                    NamespaceType.OpcPlcApplications);

                AddNodes(folder);
                AddMethods(methodsFolder);
            }
        }

        public void StartSimulation()
        {
            if (_isEnabled)
            {
                _productCounterCycleInPhase = PlcSimulation.SimulationCycleCount;
                _temperatureCycleInPhase = PlcSimulation.SimulationCycleCount;

                _productCounterNode.Start(ProductCounterGenerator, PlcSimulation.SimulationCycleLength);
                _temperatureNode.Start(TemperatureGenerator, PlcSimulation.SimulationCycleLength);
            }
        }

        public void StopSimulation()
        {
            if (_isEnabled)
            {
                _productCounterNode.Stop();
                _temperatureNode.Stop();
            }
        }

        private void AddNodes(FolderState folder)
        {
            _productCounterNode = _plcNodeManager.CreateVariableNode<uint>(
                _plcNodeManager.CreateBaseVariable(
                    folder,
                    path: "ProductCounter",
                    name: "ProductCounter",
                    new NodeId((uint)BuiltInType.UInt32),
                    ValueRanks.Scalar,
                    AccessLevels.CurrentReadOrWrite,
                    "Product counter",
                    NamespaceType.OpcPlcApplications));

            _temperatureNode = _plcNodeManager.CreateVariableNode<double>(
                _plcNodeManager.CreateBaseVariable(
                    folder,
                    path: "Temperature",
                    name: "Temperature",
                    new NodeId((uint)BuiltInType.Double),
                    ValueRanks.Scalar,
                    AccessLevels.CurrentRead,
                    "Temperature value",
                    NamespaceType.OpcPlcApplications));

            Nodes = new List<NodeWithIntervals>
            {
                new NodeWithIntervals
                {
                    NodeId = "ProductCounter",
                    Namespace = OpcPlc.Namespaces.OpcPlcApplications,
                },
                new NodeWithIntervals
                {
                    NodeId = "Temperature",
                    Namespace = OpcPlc.Namespaces.OpcPlcApplications,
                },
            };
        }

        private void AddMethods(FolderState parentFolder)
        {
            MethodState resetProductCounterMethod = _plcNodeManager.CreateMethod(parentFolder, "Reset", "ResetProductCounter", "Resets the product counter to 0", NamespaceType.OpcPlcApplications);
            resetProductCounterMethod.OnCallMethod += OnResetProductCounterCall;
        }


        /// <summary>
        /// Method to reset the product counter. Executes synchronously.
        /// </summary>
        private ServiceResult OnResetProductCounterCall(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            ResetProductCounter();
            Logger.Debug("ResetProductCounter method called");
            return ServiceResult.Good;
        }

        /// <summary>
        /// Updates simulation values. Called each SimulationCycleLength msec.
        /// </summary>
        private uint ProductCounterGenerator(uint value)
        {
            ++value;
            return value;
        }

        /// <summary>
        /// Updates simulation values. Called each SimulationCycleLength msec.
        /// Using SimulationCycleCount cycles per simulation phase.
        /// </summary>
        private double TemperatureGenerator(double value)
        {
            var offset = 30.0;
            var delta = 2.0;
            // calculate next boolean value
            value = delta * Math.Sin(((2 * Math.PI) / PlcSimulation.SimulationCycleCount) * _temperatureCycleInPhase) + 0.5 * _random.NextDouble() + offset;

            // end of cycle: reset cycle count
            if (--_temperatureCycleInPhase == 0)
            {
                _temperatureCycleInPhase = PlcSimulation.SimulationCycleCount;
            }

            return value;
        }

        /// <summary>
        /// Method implementation to reset the StepUp data.
        /// </summary>
        public void ResetProductCounter()
        {
            _productCounterNode.Value = 0;
        }
    }
}
