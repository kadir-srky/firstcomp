using UnityEngine;
using System.Collections;

// Nesnenin ne tür bir etkileşime gireceğini belirliyoruz
public enum InteractType
{
    Door,
    Key,
    Puzzle,
    Note // YENİ: Okunabilir notlar ve bilmeceler için
}

public class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    public InteractType type;
    public string promptMessage = "Interact";
    
    [Header("Note/Riddle Settings (For Notes)")]
    [TextArea(3, 10)]
    public string noteContent = "Buraya korkunç bir bilmece yaz..."; // Bilmecenin yazılacağı metin kutusu

    [Header("Lock Settings (For Doors)")]
    public bool isLocked = false;
    public string requiredKeyId = ""; // Kapıyı açmak için gereken anahtar ID'si

    [Header("Key Settings (For Keys)")]
    public string keyId = ""; // Bu anahtarın eşsiz ID'si

    [Header("Door Rotation Settings")]
    public float openAngle = 90f;
    public float openSpeed = 2f;
    
    private bool _isOpen = false;
    private Quaternion _startRotation;
    private Quaternion _targetRotation;

    void Start()
    {
        // Kapının açılma animasyonu için başlangıç ve hedef rotasyonlarını kaydet
        _startRotation = transform.localRotation;
        _targetRotation = _startRotation * Quaternion.Euler(0, openAngle, 0);
    }

    public void Interact(InteractionSystem playerSystem)
    {
        if (type == InteractType.Key)
        {
            // Anahtarı oyuncunun envanterine ekle ve sahneden yok et
            playerSystem.AddKey(keyId);
            Destroy(gameObject);
        }
        else if (type == InteractType.Note)
        {
            // YENİ: Oyuncu nota tıkladığında notu ekranda göster
            playerSystem.ShowNote(noteContent);
        }
        else if (type == InteractType.Door)
        {
            if (isLocked)
            {
                // Oyuncunun doğru anahtara sahip olup olmadığını kontrol et
                if (playerSystem.HasKey(requiredKeyId))
                {
                    isLocked = false;
                    _isOpen = true;
                    StartCoroutine(AnimateDoorOpen());
                }
            }
            else
            {
                // Kilitli değilse ve kapalıysa doğrudan aç
                if (!_isOpen)
                {
                    _isOpen = true;
                    StartCoroutine(AnimateDoorOpen());
                }
            }
        }
    }

    // Kapıyı yumuşak bir şekilde (Slerp) döndürerek açan animasyon kodu
    private IEnumerator AnimateDoorOpen()
    {
        float progress = 0f;
        while (progress < 1f)
        {
            progress += Time.deltaTime * openSpeed;
            transform.localRotation = Quaternion.Slerp(_startRotation, _targetRotation, progress);
            yield return null;
        }
    }
}