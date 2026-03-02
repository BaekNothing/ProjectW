using UnityEngine;
using UnityEngine.UI;

namespace ProjectW.IngameMvp
{
    public sealed class RoutineNeuronPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text intentText;
        [SerializeField] private Text reasonText;
        [SerializeField] private Text conditionText;
        [SerializeField] private Text gaugeText;

        public void Render(RoutineNeuronPanelViewModel viewModel)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (titleText != null) titleText.text = viewModel.Title;
            if (intentText != null) intentText.text = viewModel.IntentLine;
            if (reasonText != null) reasonText.text = viewModel.ReasonLine;
            if (conditionText != null) conditionText.text = viewModel.ConditionLine;
            if (gaugeText != null) gaugeText.text = viewModel.GaugeLine;
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
