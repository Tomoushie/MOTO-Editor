// Moto.Editor/Views/Home/HomeView.xaml.cs — dans le constructeur
void AttachChipHover(Border chip)
{
    var pointer = new PointerGestureRecognizer();
    pointer.PointerEntered += (_, _) => VisualStateManager.GoToState(chip, "PointerOver");
    pointer.PointerExited  += (_, _) => VisualStateManager.GoToState(chip, "Normal");
    pointer.PointerPressed += (_, _) => VisualStateManager.GoToState(chip, "Pressed");
    pointer.PointerReleased+= (_, _) => VisualStateManager.GoToState(chip, "PointerOver");
    chip.GestureRecognizers.Add(pointer);
}
