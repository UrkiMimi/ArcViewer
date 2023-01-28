using UnityEngine;

public class BackButton : MonoBehaviour
{
    public bool justClosed;

    public void SetMapSelection(DialogueResponse response)
    {
        if(response == DialogueResponse.Yes)
        {
            UIStateManager.CurrentState = UIState.MapSelection;
        }
        else
        {
            justClosed = true;
        }
    }


    public void ShowDialogue()
    {
        DialogueHandler.ShowDialogueBox("Are you sure you want to exit the map?", SetMapSelection, DialogueBoxType.YesNo);
    }


    private void Update()
    {
        if(Input.GetButtonDown("Cancel") && UIStateManager.CurrentState == UIState.Previewer && !DialogueHandler.DialogueActive && !justClosed)
        {
            ShowDialogue();
        }
        else justClosed = false;
    }
}