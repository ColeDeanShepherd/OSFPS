using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using NetworkLibrary;

public class StateSynchronizationExperiment : MonoBehaviour
{
    /*
     * need to support
     * ===============
     * -value types DONE
     * -nullable value types DONE
     * -reference types DONE
     * -non-nullable reference types DONE
     * -enumerations DONE
     * -collections
     * -dictionaries
    */
    public struct DummyStruct
    {
        public int Asdf;
    }
    public class DummyClass
    {
        public int Fdsa;

        public DummyClass DeepCopy()
        {
            return (DummyClass)MemberwiseClone();
        }
    }
    public class State
    {
        // currently testing
        public DummyClass[] ADC = new DummyClass[2];
        public List<DummyClass> LDC = new List<DummyClass>();

        // already tested
        public bool B;
        public byte U8;
        public ushort U16;
        public uint U32;
        public ulong U64;
        public sbyte S8;
        public short S16;
        public int S32;
        public long S64;
        public char C = '0';
        public float F32;
        public double F64;
        public decimal D;
        public DummyStruct Dummy;

        public uint? NU32;
        public DummyStruct? NDummy;

        public DummyClass DummyC;

        [NonNullable]
        public DummyClass NDummyC = new DummyClass();

        public WeaponType WeaponType = WeaponType.Pistol;

        public float[] AF = new float[2];
        public List<float> LF = new List<float>();

        public State DeepCopy()
        {
            var newState = (State)MemberwiseClone();

            newState.DummyC = DummyC?.DeepCopy();
            newState.NDummyC = NDummyC.DeepCopy();
            newState.LF = new List<float>(LF);

            newState.AF = new float[AF.Length];
            Array.Copy(AF, newState.AF, AF.Length);

            newState.LDC = new List<DummyClass>(LDC);

            newState.ADC = new DummyClass[ADC.Length];
            Array.Copy(ADC, newState.ADC, ADC.Length);

            return newState;
        }
    }

    State serverState;
    State clientState;
    NetworkedComponentTypeInfo networkedComponentTypeInfo;
    ThrottledAction sendUpdateAction;
    byte[] SentUpdate;

    private void Awake()
    {
        serverState = new State();
        clientState = new State();
        networkedComponentTypeInfo = NetLib.GetNetworkedComponentTypeInfo(typeof(State));
        sendUpdateAction = new ThrottledAction(SendUpdate, 0.25f);
    }
    private void Update()
    {
        sendUpdateAction.TryToCall();

        if (SentUpdate != null)
        {
            ReadUpdate();
        }
    }

    private State lastAcknowledgedState = new State();
    private void SendUpdate()
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(memoryStream))
            {
                NetworkSerializationUtils.SerializeDelta(writer, networkedComponentTypeInfo, lastAcknowledgedState, serverState);
            }

            SentUpdate = memoryStream.ToArray();
            OsFps.Logger.Log("Packet Size: " + SentUpdate.Length);
        }
        
        lastAcknowledgedState = serverState.DeepCopy();
    }
    private void ReadUpdate()
    {
        using (var memoryStream = new MemoryStream(SentUpdate))
        {
            using (var reader = new BinaryReader(memoryStream))
            {
                NetworkSerializationUtils.DeserializeDelta(reader, networkedComponentTypeInfo, clientState);
            }
        }

        SentUpdate = null;
    }

    private void OnGUI()
    {
        DrawState(serverState, new Vector2(10, 100));
        DrawState(clientState, new Vector2(300, 100));

        if(GUI.Button(new Rect(10, 10, 100, 40), "Change"))
        {
            OnChange();
        }
    }
    private void DrawState(State s, Vector2 position)
    {
        GUI.Label(new Rect(position, new Vector2(300, 600)), JsonUtils.ToPrettyJson(s));
    }
    private void OnChange()
    {
        serverState.B = !serverState.B;
        serverState.U8++;
        serverState.U16++;
        serverState.U32++;
        serverState.U64++;
        serverState.S8++;
        serverState.S16++;
        serverState.S32++;
        serverState.S64++;
        serverState.F32 += 0.1f;
        serverState.F64 += 0.1;
        serverState.C = (char)(serverState.C + 1);
        serverState.D += 0.1m;
        serverState.NU32 = (serverState.NU32 == null) ? 5 : (uint?)null;
        serverState.Dummy.Asdf++;
        serverState.NDummy = (serverState.NDummy == null) ? new DummyStruct() : (DummyStruct?)null;
        serverState.NDummyC.Fdsa++;
        serverState.DummyC = (serverState.DummyC == null) ? new DummyClass() : null;
        serverState.WeaponType = (serverState.WeaponType == WeaponType.Pistol) ? WeaponType.RocketLauncher : WeaponType.Pistol;
        serverState.LF.Add(1.5f);
        serverState.AF[0]++;

        serverState.LDC.Add(null);
    }
}