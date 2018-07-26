using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MenuStack : IEnumerable<MonoBehaviour>
{
    public MenuStack()
    {
        monoBehaviourStack = new Stack<MonoBehaviour>();
    }

    public void Push(MonoBehaviour menuComponent)
    {
        if (monoBehaviourStack.Any())
        {
            monoBehaviourStack.Peek().gameObject.SetActive(false);
        }

        monoBehaviourStack.Push(menuComponent);
    }
    public void Pop()
    {
        if (monoBehaviourStack.Any())
        {
            Object.Destroy(monoBehaviourStack.Peek().gameObject);
            monoBehaviourStack.Pop();
        }

        if (monoBehaviourStack.Any())
        {
            monoBehaviourStack.Peek().gameObject.SetActive(true);
        }
    }
    public MonoBehaviour Peek()
    {
        return monoBehaviourStack.Peek();
    }

    public IEnumerator<MonoBehaviour> GetEnumerator()
    {
        return monoBehaviourStack.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return monoBehaviourStack.GetEnumerator();
    }

    private Stack<MonoBehaviour> monoBehaviourStack;
}