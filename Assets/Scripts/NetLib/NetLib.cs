using System;
using System.Collections.Generic;
using System.Linq;

namespace NetworkLibrary
{
    public static class NetLib
    {
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

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                var methodBindingFlags =
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance;

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
                type.GetFields()
                    .Where(field => !Attribute.IsDefined(field, typeof(NotNetworkSynchronizedAttribute)))
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
                type.GetProperties()
                    .Where(property =>
                        property.CanRead &&
                        property.CanWrite &&
                        !Attribute.IsDefined(property, typeof(NotNetworkSynchronizedAttribute))
                    )
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
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance
                )
                .FirstOrDefault(IsApplyStateFromServerMethod);
            var stateField = monoBehaviourType.GetFields()
                .FirstOrDefault(f => f.FieldType.IsEquivalentTo(type));

            return new NetworkedComponentTypeInfo
            {
                StateType = type,
                StateIdField = type.GetFields().FirstOrDefault(fieldInfo => fieldInfo.Name == "Id"),
                ThingsToSynchronize = thingsToSynchronize,
                MonoBehaviourType = monoBehaviourType,
                MonoBehaviourStateField = stateField,
                MonoBehaviourApplyStateMethod = applyStateMethod,
                SynchronizedComponentAttribute = synchronizedComponentAttribute
            };
        }
        private static bool IsApplyStateFromServerMethod(System.Reflection.MethodInfo methodInfo)
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
            var networkedComponentStateLists = new List<List<object>>(networkedComponentTypeInfos.Count);
            foreach (var networkedComponentTypeInfo in networkedComponentTypeInfos)
            {
                networkedComponentStateLists.Add(new List<object>());
            }

            return new NetworkedGameState
            {
                SequenceNumber = 0,
                NetworkedComponentTypeInfos = networkedComponentTypeInfos,
                NetworkedComponentStateLists = networkedComponentStateLists
            };
        }
        public static NetworkedGameState GetCurrentNetworkedGameState(bool generateSequenceNumber)
        {
            var sequenceNumber = generateSequenceNumber ? GenerateGameStateSequenceNumber() : 0;
            var networkedGameState = new NetworkedGameState
            {
                SequenceNumber = sequenceNumber,
                NetworkedComponentTypeInfos = networkedComponentTypeInfos,
                NetworkedComponentStateLists = GetComponentStateListsToSynchronize(networkedComponentTypeInfos)
            };

            return networkedGameState;
        }
        public static List<List<object>> GetComponentStateListsToSynchronize(List<NetworkedComponentTypeInfo> networkedComponentTypeInfos)
        {
            return networkedComponentTypeInfos
                .Select(GetStateObjectsToSynchronize)
                .ToList();
        }
        public static List<object> GetStateObjectsToSynchronize(NetworkedComponentTypeInfo networkedComponentTypeInfo)
        {
            var monoBehaviours = UnityEngine.Object.FindObjectsOfType(networkedComponentTypeInfo.MonoBehaviourType);

            return monoBehaviours
                .Select(mb => networkedComponentTypeInfo.MonoBehaviourStateField.GetValue(mb))
                .ToList();
        }

        public static void ExecuteRpc(byte id, object serverObj, object clientObj, params object[] arguments)
        {
            var rpcInfo = rpcInfoById[id];
            var objContainingRpc = (rpcInfo.ExecuteOn == NetworkLibrary.NetworkPeerType.Server)
                ? serverObj
                : clientObj;
            rpcInfo.MethodInfo.Invoke(objContainingRpc, arguments);

            OsFps.Logger.Log($"Executed RPC {rpcInfo.Name}");
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
            var monoBehaviour = (UnityEngine.MonoBehaviour)UnityEngine.Object.FindObjectsOfType(networkedComponentTypeInfo.MonoBehaviourType)
                .FirstOrDefault(o =>
                {
                    var state = networkedComponentTypeInfo.MonoBehaviourStateField.GetValue(o);
                    var currentStateId = GetIdFromState(networkedComponentTypeInfo, state);

                    return currentStateId == stateId;
                });

            return monoBehaviour;
        }

        public static uint GetIdFromState(NetworkedComponentTypeInfo networkedComponentTypeInfo, object state)
        {
            return (uint)networkedComponentTypeInfo.StateIdField.GetValue(state);
        }
        
        private static uint _nextGameStateSequenceNumber = 1;
        public static uint GenerateGameStateSequenceNumber()
        {
            var generatedGameStateSequenceNumber = _nextGameStateSequenceNumber;
            _nextGameStateSequenceNumber++;
            return generatedGameStateSequenceNumber;
        }
    }
}