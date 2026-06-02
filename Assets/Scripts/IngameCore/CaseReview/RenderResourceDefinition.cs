using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
[CreateAssetMenu(menuName = "ProjectW/Case Review/Render Resources", fileName = "RenderResources")]
public sealed class RenderResourceDefinition : ScriptableObject
{
    [SerializeField] private string resourceId = "";
    [SerializeField] private string displayLabel = "";
    [SerializeField] private Sprite icon;
    [SerializeField] private Sprite portrait;
    [SerializeField] private Sprite cardArt;
    [SerializeField] private GameObject prefab;
    [SerializeField] private Material material;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private AudioClip voiceCue;
    [SerializeField] private AudioClip sfxCue;
    [SerializeField] private Color accentColor = Color.white;
    [SerializeField] private string addressableKey = "";
    [SerializeField] private string uiVariant = "";
    [TextArea(2, 5)] [SerializeField] private string note = "";

    public string ResourceId => resourceId;
    public string DisplayLabel => displayLabel;
    public Sprite Icon => icon;
    public Sprite Portrait => portrait;
    public Sprite CardArt => cardArt;
    public GameObject Prefab => prefab;
    public Material Material => material;
    public RuntimeAnimatorController AnimatorController => animatorController;
    public AudioClip VoiceCue => voiceCue;
    public AudioClip SfxCue => sfxCue;
    public Color AccentColor => accentColor;
    public string AddressableKey => addressableKey;
    public string UiVariant => uiVariant;
    public string Note => note;
}
}
