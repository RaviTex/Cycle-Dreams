using System.Runtime.InteropServices;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

[StructLayout(LayoutKind.Explicit, Size = 20)]
public struct BikeState : IInputStateTypeInfo
{
    public FourCC format => new FourCC('H', 'I', 'D', '\0');

    [InputControl(name = "speed", layout = "Axis",
                  offset = 3, sizeInBits = 16,
                  parameters = "normalize=true,normalizeMin=0,normalizeMax=32767")]
    [FieldOffset(3)]
    public ushort speed;
}