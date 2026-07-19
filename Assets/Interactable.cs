using System.Collections;
using UnityEngine;

public enum InteractType
{
    Door,
    Key,
    Puzzle,
    Note
}

public class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    public InteractType type;
    public string promptMessage = "Interact";

    [Header("Note/Riddle Settings (For Notes)")]
    [TextArea(3, 10)]
    public string noteContent = "Buraya korkunc bir bilmece yaz...";

    [Header("Lock Settings (For Doors)")]
    public bool isLocked;
    public string requiredKeyId = "";

    [Header("Key Settings (For Keys)")]
    public string keyId = "";

    [Header("Door Rotation Settings")]
    public float openAngle = 90f;
    public float openSpeed = 2f;

    private bool _isOpen;
    private Quaternion _startRotation;
    private Quaternion _targetRotation;
    private Coroutine _doorAnimation;

    private void Start()
    {
        _startRotation = transform.localRotation;
        _targetRotation = _startRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    public void Interact(InteractionSystem playerSystem)
    {
        if (type == InteractType.Key)
        {
            playerSystem.AddKey(keyId);
            Destroy(gameObject);
            return;
        }

        if (type == InteractType.Note)
        {
            playerSystem.ShowNote(noteContent);
            return;
        }

        if (type != InteractType.Door)
            return;

        if (isLocked)
        {
            if (!playerSystem.HasKey(requiredKeyId))
                return;

            isLocked = false;
        }

        _isOpen = !_isOpen;
        if (_doorAnimation != null)
            StopCoroutine(_doorAnimation);
        _doorAnimation = StartCoroutine(AnimateDoor(_isOpen ? _targetRotation : _startRotation));
    }

    private IEnumerator AnimateDoor(Quaternion destination)
    {
        var initialRotation = transform.localRotation;
        var progress = 0f;
        while (progress < 1f)
        {
            progress += Time.deltaTime * openSpeed;
            transform.localRotation = Quaternion.Slerp(initialRotation, destination, progress);
            yield return null;
        }

        transform.localRotation = destination;
        _doorAnimation = null;
    }
}
