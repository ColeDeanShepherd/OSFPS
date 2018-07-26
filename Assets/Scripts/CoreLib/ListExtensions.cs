using System.Linq;
using System.Collections.Generic;

public static class ListExtensions
{
    public static void GetChanges<T>(
        List<T> oldList,
        List<T> newList,
        System.Func<T, T, bool> doElementsMatch,
        out List<T> removedElements,
        out List<T> addedElements,
        out List<T> updatedElements
    )
    {
        removedElements = oldList.Where(oldElement =>
            !newList.Any(newElement => doElementsMatch(oldElement, newElement))
        ).ToList();
        addedElements = newList.Where(newElement =>
            !oldList.Any(oldElement => doElementsMatch(oldElement, newElement))
        ).ToList();
        updatedElements = newList.Where(newElement =>
            oldList.Any(oldElement => doElementsMatch(oldElement, newElement))
        ).ToList();
    }

    public static void AppendWithMaxLength<T>(List<T> list, T elementToAppend, int maxLength)
    {
        if (maxLength == 0) return;

        if (list.Count < maxLength)
        {
            list.Add(elementToAppend);
        }
        else if (list.Count == maxLength)
        {
            // shift the elements down
            for (var i = 0; i < list.Count - 1; i++)
            {
                list[i] = list[i + 1];
            }

            // "append" the new element
            list[list.Count - 1] = elementToAppend;
        }
        else
        {
            throw new System.NotImplementedException();
        }
    }
}