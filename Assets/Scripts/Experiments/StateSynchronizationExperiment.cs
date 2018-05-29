using System.IO;
using UnityEngine;

public class StateSynchronizationExperiment : MonoBehaviour
{
    public struct State
    {
        public uint id;
        public string name;
        public bool a;
        public char b;
        public int c;
        public float d;
        public double? e;
    }

    State serverState;
    State clientState;

    ThrottledAction sendUpdateAction;
    byte[] SentUpdate;

    private void Awake()
    {
        var asdf = new GrenadeComponent();
        serverState = new State
        {
            id = 1,
            name = "Server",
            a = false,
            b = 't',
            c = -4,
            d = 3.14f,
            e = null
        };

        clientState = serverState;
        clientState.name = "Client";

        sendUpdateAction = new ThrottledAction(SendUpdate, 0.25f);
    }
    private void Start()
    {

    }
    private void Update()
    {
        serverState.d += Time.deltaTime;

        sendUpdateAction.TryToCall();

        if (SentUpdate != null)
        {
            ReadUpdate();
        }
    }

    private void SendUpdate()
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(memoryStream))
            {
                NetworkSerializationUtils.Serialize(writer, serverState);
            }

            SentUpdate = memoryStream.ToArray();
        }
    }
    private void ReadUpdate()
    {
        using (var memoryStream = new MemoryStream(SentUpdate))
        {
            using (var reader = new BinaryReader(memoryStream))
            {
                clientState = NetworkSerializationUtils.Deserialize<State>(reader);
            }
        }

        SentUpdate = null;
    }

    private void OnGUI()
    {
        DrawState(serverState, new Vector2(10, 10));
        DrawState(clientState, new Vector2(150, 10));
    }
    private void DrawState(State s, Vector2 position)
    {
        var fieldInfos = s.GetType().GetFields();
        var fieldSize = new Vector2(300, 20);
        var curFieldPos = position;

        for (var i = 0; i < fieldInfos.Length; i++)
        {
            var fieldInfo = fieldInfos[i];
            var fieldValue = fieldInfo.GetValue(s);
            var fieldValueStr = (fieldValue != null) ? fieldValue.ToString() : "null";

            GUI.Label(new Rect(curFieldPos, fieldSize), $"{fieldInfo.Name}: {fieldValueStr}");

            curFieldPos.y += fieldSize.y;
        }
    }
}