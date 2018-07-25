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
}