/// Credit Ziboo
/// Sourced from - http://forum.unity3d.com/threads/free-reorderable-list.364600/

namespace UnityEngine.UI.Extensions
{
    public class ReorderableListDebug : MonoBehaviour
    {
        

        void Awake()
        {
            foreach (var list in FindObjectsOfType<ReorderableList>())
            {
                list.OnElementDropped.AddListener(ElementDropped);
            }
        }

        private void ElementDropped(ReorderableList.ReorderableListEventStruct droppedStruct)
        {
            string txt;
            txt = "";
            txt += "Dropped Object: " + droppedStruct.DroppedObject.name + "\n";
            txt += "Is Clone ?: " + droppedStruct.IsAClone + "\n";
            if (droppedStruct.IsAClone)
                txt += "Source Object: " + droppedStruct.SourceObject.name + "\n";
            txt += string.Format("From {0} at Index {1} \n", droppedStruct.FromList.name, droppedStruct.FromIndex);
            txt += string.Format("To {0} at Index {1} \n", droppedStruct.ToList == null ? "Empty space" : droppedStruct.ToList.name, droppedStruct.ToIndex);
            Debug.Log(txt);
        }
    }
}