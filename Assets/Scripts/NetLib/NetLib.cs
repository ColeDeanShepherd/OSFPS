using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Networking;

namespace NetworkLibrary
{
    public static class NetLib
    {
        public const string LocalHostIpv4Address = "127.0.0.1";
        public const byte StateSynchronizationMessageId = 0;
        public const bool EnableRpcLogging = false;
        public const bool EnableStateDeltaLogging = false;

        public const string ApplyStateMethodName = "ApplyStateFromServer";
        public static bool ParseIpAddressAndPort(
            string ipAddressAndPort, ushort defaultPortNumber, out string ipAddress, out ushort portNumber
        )
        {
            var splitIpAddressAndPort = ipAddressAndPort.Split(new[] { ':' });

            if (splitIpAddressAndPort.Length == 1)
            {
                ipAddress = splitIpAddressAndPort[0];
                portNumber = defaultPortNumber;
                return true;

            }
            else if (splitIpAddressAndPort.Length == 2)
            {
                ipAddress = splitIpAddressAndPort[0];
                return ushort.TryParse(splitIpAddressAndPort[1], out portNumber);
            }
            else
            {
                ipAddress = "";
                portNumber = 0;
                return false;
            }
        }

        public static Dictionary<string, byte> rpcIdByName;
        public static Dictionary<byte, RpcInfo> rpcInfoById;
        public static List<NetworkedComponentTypeInfo> networkedComponentTypeInfos;
        public static void Setup()
        {
            GetRpcInfo(out rpcIdByName, out rpcInfoById);
            networkedComponentTypeInfos = GetNetworkedComponentTypeInfos();
        }

        public static void GetRpcInfo(out Dictionary<string, byte> rpcIdByName, out Dictionary<byte, RpcInfo> rpcInfoById)
        {
            rpcIdByName = new Dictionary<string, byte>();
            rpcInfoById = new Dictionary<byte, RpcInfo>();

            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                var methodBindingFlags =
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance;

                foreach (var methodInfo in type.GetMethods(methodBindingFlags))
                {
                    var rpcAttribute = (RpcAttribute)methodInfo.GetCustomAttributes(typeof(RpcAttribute), inherit: false)
                        .FirstOrDefault();
                    var parameterInfos = methodInfo.GetParameters();

                    if (rpcAttribute != null)
                    {
                        var rpcInfo = new RpcInfo
                        {
                            Id = (byte)(1 + rpcInfoById.Count),
                            Name = methodInfo.Name,
                            ExecuteOn = rpcAttribute.ExecuteOn,
                            MethodInfo = methodInfo,
                            ParameterNames = parameterInfos
                                .Select(parameterInfo => parameterInfo.Name)
                                .ToArray(),
                            ParameterTypes = parameterInfos
                                .Select(parameterInfo => parameterInfo.ParameterType)
                                .ToArray()
                        };

                        rpcIdByName.Add(rpcInfo.Name, rpcInfo.Id);
                        rpcInfoById.Add(rpcInfo.Id, rpcInfo);
                    }
                }
            }
        }

        public static List<NetworkedComponentTypeInfo> GetNetworkedComponentTypeInfos()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            return assembly.GetTypes()
                .Select(GetNetworkedComponentTypeInfo)
                .Where(t => t != null)
                .ToList();
        }
        public static NetworkedComponentTypeInfo GetNetworkedComponentTypeInfo(Type type)
        {
            var synchronizedComponentAttribute = (NetworkedComponentAttribute)type.GetCustomAttributes(
                           typeof(NetworkedComponentAttribute), inherit: false
                       ).FirstOrDefault();

            if (synchronizedComponentAttribute == null)
            {
                return null;
            }

            var thingsToSynchronize = new List<NetworkedTypeFieldInfo>();

            thingsToSynchronize.AddRange(
                GetFieldInfosToSerialize(type)
                    .Select(fieldInfo => new NetworkedTypeFieldInfo
                    {
                        FieldInfo = fieldInfo,
                        IsNullableIfReferenceType =
                            fieldInfo.FieldType.IsClass &&
                            !Attribute.IsDefined(fieldInfo, typeof(NonNullableAttribute)),
                        AreElementsNullableIfReferenceType = !Attribute.IsDefined(fieldInfo, typeof(NonNullableElementAttribute))
                    })
            );
            
            thingsToSynchronize.AddRange(
                GetPropertyInfosToSerialize(type)
                    .Select(propertyInfo => new NetworkedTypeFieldInfo
                    {
                        PropertyInfo = propertyInfo,
                        IsNullableIfReferenceType =
                            propertyInfo.PropertyType.IsClass
                            && !Attribute.IsDefined(propertyInfo, typeof(NonNullableAttribute)),
                        AreElementsNullableIfReferenceType = !Attribute.IsDefined(propertyInfo, typeof(NonNullableElementAttribute))
                    })
            );

            var monoBehaviourType = synchronizedComponentAttribute.MonoBehaviourType;
            var applyStateMethod = monoBehaviourType.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance
                )
                .FirstOrDefault(IsApplyStateFromServerMethod);
            var stateField = monoBehaviourType.GetFields()
                .FirstOrDefault(f => f.FieldType.IsEquivalentTo(type));
            var monoBehaviourInstancesField = monoBehaviourType.GetField("Instances");

            return new NetworkedComponentTypeInfo
            {
                StateType = type,
                StateIdField = type.GetFields().FirstOrDefault(fieldInfo => fieldInfo.Name == "Id"),
                ThingsToSynchronize = thingsToSynchronize,
                MonoBehaviourType = monoBehaviourType,
                MonoBehaviourStateField = stateField,
                MonoBehaviourApplyStateMethod = applyStateMethod,
                MonoBehaviourInstancesField = monoBehaviourInstancesField,
                SynchronizedComponentAttribute = synchronizedComponentAttribute
            };
        }
        private static bool IsApplyStateFromServerMethod(MethodInfo methodInfo)
        {
            if (methodInfo.Name != ApplyStateMethodName) return false;

            var parameters = methodInfo.GetParameters();
            if (parameters.Length != 1) return false;

            var newStateParameter = parameters[0];
            if (!newStateParameter.ParameterType.IsEquivalentTo(typeof(object))) return false;

            return true;
        }
        
        public static NetworkedGameState GetEmptyNetworkedGameStateForDiffing()
        {
            var networkedComponentInfoLists = new List<List<NetworkedComponentInfo>>(networkedComponentTypeInfos.Count);
            foreach (var networkedComponentTypeInfo in networkedComponentTypeInfos)
            {
                networkedComponentInfoLists.Add(new List<NetworkedComponentInfo>());
            }

            return new NetworkedGameState
            {
                SequenceNumber = 0,
                NetworkedComponentTypeInfos = networkedComponentTypeInfos,
                NetworkedComponentInfoLists = networkedComponentInfoLists
            };
        }
        public static NetworkedGameState GetCurrentNetworkedGameState(bool generateSequenceNumber)
        {
            var sequenceNumber = generateSequenceNumber ? GenerateGameStateSequenceNumber() : 0;
            var networkedGameState = new NetworkedGameState
            {
                SequenceNumber = sequenceNumber,
                NetworkedComponentTypeInfos = networkedComponentTypeInfos,
                NetworkedComponentInfoLists = GetComponentInfosToSynchronize(networkedComponentTypeInfos)
            };

            return networkedGameState;
        }
        public static List<List<NetworkedComponentInfo>> GetComponentInfosToSynchronize(List<NetworkedComponentTypeInfo> networkedComponentTypeInfos)
        {
            return networkedComponentTypeInfos
                .Select(networkedComponentTypeInfo =>
                {
                    var monoBehaviours = (ICollection)networkedComponentTypeInfo.MonoBehaviourInstancesField.GetValue(null);

                    var componentInfos = new List<NetworkedComponentInfo>(monoBehaviours.Count);
                    foreach (var monoBehaviour in monoBehaviours)
                    {
                        if (monoBehaviour == null) continue;

                        var componentState = networkedComponentTypeInfo.MonoBehaviourStateField.GetValue(monoBehaviour);
                        componentInfos.Add(new NetworkedComponentInfo
                        {
                            ComponentState = ObjectExtensions.DeepCopy(componentState)
                        });
                    }

                    return componentInfos;
                })
                .ToList();
        }

        public static void ExecuteRpc(byte id, object serverObj, object clientObj, params object[] arguments)
        {
            var rpcInfo = rpcInfoById[id];
            var objContainingRpc = (rpcInfo.ExecuteOn == NetworkLibrary.NetworkPeerType.Server)
                ? serverObj
                : clientObj;
            rpcInfo.MethodInfo.Invoke(objContainingRpc, arguments);
        }

        public static UnityEngine.MonoBehaviour GetMonoBehaviourByState(NetworkedComponentTypeInfo networkedComponentTypeInfo, object state)
        {
            var stateId = GetIdFromState(networkedComponentTypeInfo, state);
            return GetMonoBehaviourByStateId(networkedComponentTypeInfo, stateId);
        }
        public static UnityEngine.MonoBehaviour GetMonoBehaviourByStateId(
            NetworkedComponentTypeInfo networkedComponentTypeInfo, uint stateId
        )
        {
            var monoBehaviourObjects = (ICollection)networkedComponentTypeInfo.MonoBehaviourInstancesField.GetValue(null);
            foreach (var obj in monoBehaviourObjects)
            {
                var state = networkedComponentTypeInfo.MonoBehaviourStateField.GetValue(obj);
                var currentStateId = GetIdFromState(networkedComponentTypeInfo, state);

                if(currentStateId == stateId)
                {
                    return (UnityEngine.MonoBehaviour)obj;
                }
            }

            return null;
        }

        public static uint GetIdFromState(NetworkedComponentTypeInfo networkedComponentTypeInfo, object state)
        {
            UnityEngine.Profiling.Profiler.BeginSample("GetIdFromState");
            var id = (uint)networkedComponentTypeInfo.StateIdField.GetValue(state);
            UnityEngine.Profiling.Profiler.EndSample();
            return id;
        }

        public static ConnectionConfig CreateConnectionConfig(
            out int reliableSequencedChannelId,
            out int reliableChannelId,
            out int unreliableStateUpdateChannelId,
            out int unreliableFragmentedChannelId,
            out int unreliableChannelId
        )
        {
            var connectionConfig = new ConnectionConfig();
            reliableSequencedChannelId = connectionConfig.AddChannel(QosType.ReliableSequenced);
            reliableChannelId = connectionConfig.AddChannel(QosType.Reliable);
            unreliableStateUpdateChannelId = connectionConfig.AddChannel(QosType.StateUpdate);
            unreliableFragmentedChannelId = connectionConfig.AddChannel(QosType.UnreliableFragmented);
            unreliableChannelId = connectionConfig.AddChannel(QosType.Unreliable);

            return connectionConfig;
        }


        public static void ApplyStates<StateType>(
            List<StateType> oldStates, List<StateType> newStates,
            Func<StateType, StateType, bool> doStatesHaveSameId,
            Action<StateType> removeStateObject,
            Action<StateType> addStateObject,
            Action<StateType, StateType> updateStateObject
        )
        {
            List<StateType> removedStates, addedStates, updatedStates;
            ListExtensions.GetChanges(
                oldStates, newStates, doStatesHaveSameId,
                out removedStates, out addedStates, out updatedStates
            );

            // Despawn weapon objects.
            foreach (var removedState in removedStates)
            {
                removeStateObject(removedState);
            }

            // Spawn weapon objects.
            foreach (var addedState in addedStates)
            {
                addStateObject(addedState);
            }

            // Update existing weapon objects.
            foreach (var updatedState in updatedStates)
            {
                var oldState = oldStates.First(os => doStatesHaveSameId(os, updatedState));
                updateStateObject(oldState, updatedState);
            }
        }
        public static void ApplyState(
            NetworkedComponentTypeInfo networkedComponentTypeInfo, List<NetworkedComponentInfo> oldComponentInfos,
            List<NetworkedComponentInfo> newComponentInfos, Func<object, UnityEngine.GameObject> createGameObjectFromState
        )
        {
            Func<NetworkedComponentInfo, NetworkedComponentInfo, bool> doIdsMatch =
                (nci1, nci2) => GetIdFromState(networkedComponentTypeInfo, nci1.ComponentState) == GetIdFromState(networkedComponentTypeInfo, nci2.ComponentState);

            Action<NetworkedComponentInfo> handleRemovedState = removedComponentInfo =>
            {
                var monoBehaviour = GetMonoBehaviourByState(networkedComponentTypeInfo, removedComponentInfo.ComponentState);

                UnityEngine.Object.Destroy(monoBehaviour.gameObject);
            };

            Action<NetworkedComponentInfo> handleAddedState = addedComponentInfo =>
            {
                createGameObjectFromState(addedComponentInfo.ComponentState);

                if (
                    (networkedComponentTypeInfo != null) &&
                    (networkedComponentTypeInfo.MonoBehaviourApplyStateMethod != null)
                )
                {
                    var stateId = GetIdFromState(networkedComponentTypeInfo, addedComponentInfo.ComponentState);
                    var monoBehaviour = GetMonoBehaviourByStateId(networkedComponentTypeInfo, stateId);

                    networkedComponentTypeInfo.MonoBehaviourApplyStateMethod.Invoke(monoBehaviour, new[] { addedComponentInfo.ComponentState });
                }
            };

            Action<NetworkedComponentInfo, NetworkedComponentInfo> handleUpdatedState =
                (oldComponentInfo, newComponentInfo) =>
                {
                    var oldStateId = GetIdFromState(networkedComponentTypeInfo, oldComponentInfo.ComponentState);
                    var monoBehaviour = GetMonoBehaviourByStateId(networkedComponentTypeInfo, oldStateId);

                    if (networkedComponentTypeInfo.MonoBehaviourApplyStateMethod == null)
                    {
                        networkedComponentTypeInfo.MonoBehaviourStateField.SetValue(monoBehaviour, newComponentInfo.ComponentState);
                    }
                    else
                    {
                        networkedComponentTypeInfo.MonoBehaviourApplyStateMethod?.Invoke(monoBehaviour, new[] { newComponentInfo.ComponentState });
                    }
                };

            ApplyStates(
                oldComponentInfos, newComponentInfos, doIdsMatch,
                handleRemovedState, handleAddedState, handleUpdatedState
            );
        }

        private static uint _nextGameStateSequenceNumber = 1;
        public static uint GenerateGameStateSequenceNumber()
        {
            var generatedGameStateSequenceNumber = _nextGameStateSequenceNumber;
            _nextGameStateSequenceNumber++;
            return generatedGameStateSequenceNumber;
        }

        public static IEnumerable<FieldInfo> GetFieldInfosToSerialize(Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field =>
                    !field.IsStatic &&
                    !field.IsLiteral &&
                    !Attribute.IsDefined(field, typeof(NotNetworkSynchronizedAttribute)));
        }
        public static IEnumerable<PropertyInfo> GetPropertyInfosToSerialize(Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property =>
                    property.CanRead &&
                    property.CanWrite &&
                    !Attribute.IsDefined(property, typeof(NotNetworkSynchronizedAttribute)));
        }
    }
}