using UnityEngine;
using UnityEngine.EventSystems;

public static class EventSystemHelper
{
    private static EventSystem eventSystem => EventSystem.current;


    public static void SetSelectedGameObject(GameObject selected)
    {
        if(!eventSystem.alreadySelecting)
        {
            eventSystem.SetSelectedGameObject(selected);
        }
    }


    public static GameObject SelectedObject => eventSystem.currentSelectedGameObject;
}