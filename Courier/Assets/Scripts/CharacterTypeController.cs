using System.Collections.Generic;
using UnityEngine;

public class CharacterTypeController : MonoBehaviour
{
    [SerializeField] private List<GameObject> _characters;
    
    public void SetCharacterType(int typeCharacter)
    {
        _characters[typeCharacter].SetActive(true);
        for (int i = 0; i < _characters.Count; i++)
        {
            if (i != typeCharacter)
            {
                _characters[i].SetActive(false);
            }
        }
    }
}