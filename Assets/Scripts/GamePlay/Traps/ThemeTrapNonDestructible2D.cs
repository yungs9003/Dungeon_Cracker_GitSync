using Dungeon.Core;
using UnityEngine;
namespace Dungeon.Traps
{
    public sealed class ThemeTrapNonDestructible2D : MonoBehaviour, IPlayerDestructionGate, ITrapToggle
    {
        public bool CanBeDestroyedByPlayer() => false;
        public void SetTrapEnabled(bool value) { var cliff = GetComponent<CliffTrap2D>(); if (cliff != null) cliff.enabled = value; }
    }
}
