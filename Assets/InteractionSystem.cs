using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class InteractionSystem : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;
    public Text interactionText; 
    
    [Header("UI Panels")]
    public GameObject notePanel; 
    public Text noteDisplayText; 

    [Header("Raycast Settings")]
    public float interactionDistance = 3.0f;
    public float interactionRadius = 0.5f; 
    
    private List<string> _collectedKeys = new List<string>();
    private bool _isReadingNote = false; 
    private Interactable _currentInteractable; // HATA BURADAN KAYNAKLANIYORDU, EKLENDİ!

    public Vector3 InteractionPosition => playerCamera != null ? playerCamera.position : transform.position;

    void Start()
    {
        if (interactionText != null) interactionText.text = "";
        if (notePanel != null) notePanel.SetActive(false); 
    }

    void Update()
    {
        if (_isReadingNote)
        {
            // Not okuyorsak E veya ESC ile kapat
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                CloseNote();
            }
            return;
        }
        CheckForInteractables();
    }

    void CheckForInteractables()
    {
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit[] hits = Physics.SphereCastAll(ray, interactionRadius, interactionDistance);
        Interactable closestInteractable = null;
        float closestDistance = float.MaxValue;

        // A door frame has its own collider and often sits in front of the
        // door collider.  Check every hit so the frame cannot hide the door
        // interaction behind it.
        foreach (RaycastHit hit in hits)
        {
            Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
            if (interactable != null && hit.distance < closestDistance)
            {
                closestInteractable = interactable;
                closestDistance = hit.distance;
            }
        }

        if (closestInteractable != null)
        {
            _currentInteractable = closestInteractable;
            UpdateUI(closestInteractable);

            if (Input.GetKeyDown(KeyCode.E))
                closestInteractable.Interact(this);
            return;
        }

        if (_currentInteractable != null)
        {
            _currentInteractable = null;
        }

        if (interactionText != null) interactionText.text = "";
    }

    void UpdateUI(Interactable interactable)
    {
        if (interactionText == null) return;

        if (interactable.type == InteractType.Door && interactable.isLocked)
        {
            if (HasKey(interactable.requiredKeyId))
            {
                interactionText.color = Color.green;
                interactionText.text = $"[E] Kapıyı Aç ({interactable.promptMessage})";
            }
            else
            {
                interactionText.color = Color.red;
                interactionText.text = $"Kilitli! İhtiyacın olan: {interactable.requiredKeyId}";
            }
        }
        else if (interactable.type == InteractType.Note)
        {
            interactionText.color = Color.yellow;
            interactionText.text = $"[E] Oku: {interactable.promptMessage}";
        }
        else
        {
            interactionText.color = Color.white;
            interactionText.text = $"[E] {interactable.promptMessage}";
        }
    }

    public void ShowNote(string content)
    {
        if (notePanel != null && noteDisplayText != null)
        {
            noteDisplayText.text = content;
            notePanel.SetActive(true);
            _isReadingNote = true;
            interactionText.text = ""; 
            
            // Oyuncunun farenin etrafa dönmesini engelle
            FirstPersonController fpc = GetComponent<FirstPersonController>();
            if (fpc != null) fpc.enabled = false;
        }
    }

    public void CloseNote()
    {
        if (notePanel != null)
        {
            notePanel.SetActive(false);
            _isReadingNote = false;
            
            // Oyuncunun hareketini geri ver
            FirstPersonController fpc = GetComponent<FirstPersonController>();
            if (fpc != null) fpc.enabled = true;
        }
    }

    public void AddKey(string keyId)
    {
        if (!_collectedKeys.Contains(keyId))
        {
            _collectedKeys.Add(keyId);
        }
    }

    public bool HasKey(string keyId)
    {
        return _collectedKeys.Contains(keyId);
    }
}
