using UnityEngine;
using UnityEngine.UI;

namespace ProjectW.IngameMvp
{
    public sealed class RoutineObjectInfoPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;

        public void Configure(GameObject root, Text title, Text body)
        {
            panelRoot = root;
            titleText = title;
            bodyText = body;
        }

        public void Render(string title, string body)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = body;
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }
    }
}
