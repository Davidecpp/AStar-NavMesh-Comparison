using TMPro;
using UnityEngine;

public class MemoryUsage : MonoBehaviour
{
    public TMP_Text memoryText;
    void Update()
    {
        long usedMemory = System.GC.GetTotalMemory(false); // in byte
        memoryText.text = $"Memoria usata: {usedMemory / 1024f} KB";
    }
}
