using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;

[InputControlLayout(stateType = typeof(BikeState), displayName = "Bike Speed Sensor")]
public class BikeDevice : InputDevice
{
    public AxisControl speed { get; private set; }

    protected override void FinishSetup()
    {
        base.FinishSetup();
        speed = GetChildControl<AxisControl>("speed");
    }

    public static BikeDevice current { get; private set; }

    public override void MakeCurrent()
    {
        base.MakeCurrent();
        current = this;
    }
}