using UnityEngine;

public class AOI : MonoBehaviour
{
    public string aoiId = "AOI";

    public string GetId() => string.IsNullOrWhiteSpace(aoiId) ? gameObject.name : aoiId;
}