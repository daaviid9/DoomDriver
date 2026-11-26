using UnityEngine;
using DoomDriver.Economy;

/// <summary>
/// Shows a purely cosmetic car accessory (Coconut Tree air freshener, Racing Stripes) once it is bought
/// in the KurierShop. Optionally dangles the visual like something hanging from the rearview mirror.
/// The visual must be a child object, because this component stays active to listen for purchases.
/// </summary>
public class CosmeticUpgradeView : MonoBehaviour
{
    [SerializeField] private UpgradeType upgradeType = UpgradeType.CoconutFreshener;

    [Tooltip("Child object shown when owned. Defaults to the first child.")]
    [SerializeField] private GameObject visualRoot;

    [Header("Dangle (air freshener)")]
    [SerializeField] private bool dangle = true;
    [SerializeField] private float dangleAngle = 9f;
    [SerializeField] private float dangleSpeed = 2.3f;

    private Quaternion restRotation;

    private void Awake()
    {
        if (visualRoot == null && transform.childCount > 0)
        {
            visualRoot = transform.GetChild(0).gameObject;
        }

        if (visualRoot != null) restRotation = visualRoot.transform.localRotation;
    }

    private void OnEnable()
    {
        UpgradeManager.OnUpgradesChanged += Refresh;
        Refresh();
    }

    private void Start() => Refresh();

    private void OnDisable()
    {
        UpgradeManager.OnUpgradesChanged -= Refresh;
    }

    private void Update()
    {
        if (!dangle || visualRoot == null || !visualRoot.activeSelf) return;

        float swing = Mathf.Sin(Time.time * dangleSpeed) * dangleAngle;
        float wobble = Mathf.Sin(Time.time * dangleSpeed * 0.53f) * dangleAngle * 0.4f;
        visualRoot.transform.localRotation = restRotation * Quaternion.Euler(wobble, 0f, swing);
    }

    private void Refresh()
    {
        if (visualRoot == null) return;

        bool isOwned = UpgradeManager.Instance != null && UpgradeManager.Instance.IsOwned(upgradeType);
        visualRoot.SetActive(isOwned);
    }
}
