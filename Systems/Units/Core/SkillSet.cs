// SkillSet.cs
using System;
using UnityEngine;

[Serializable]
public class SkillSet
{
    [Range(0, 10)]
    public float digging = 1f;
    [Range(0, 10)]
    public float conquering = 1f;
    [Range(0, 10)]
    public float fighting = 1f;
    // … weitere Skills …
}
