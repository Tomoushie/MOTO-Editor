// Moto.Editor/Controls/HoverEffects.cs
// ★ AJOUT (31/08) : aide partagée pour la "zone grise arrondie au survol/clic"
// demandée par Tom sur tous les boutons sous la zone de saisie et de
// l'explorateur (points 1, 3). Promue en helper public réutilisable — le même
// patron existait déjà en privé dans CustomMenuBarView.xaml.cs
// (AttachButtonFeedback, pour Min/Max/Fermer) ; centralisé ici pour ne pas le
// dupliquer à chaque nouveau bouton.
using Microsoft.Maui.Controls;

namespace Moto.Editor.Controls
{
    public static class HoverEffects
    {
        /// <summary>
        /// Colore le fond de l'élément au survol/clic (Border, Button, etc. — tout
        /// View avec BackgroundColor). Windows ne le fait pas tout seul pour
        /// nos éléments dessinés à la main (pas de contrôle natif).
        /// idleColor : couleur de repos à restaurer en sortie de survol — Transparent
        /// par défaut, mais certains boutons (ceux qui ont déjà un fond BgPanel au
        /// repos, ex. IA/Cortex/Modèle/Puissance) doivent revenir à CE fond-là, pas à
        /// Transparent, sous peine de "disparaître" visuellement une fois la souris partie.
        /// </summary>
        public static void Attach(View element, Color? hoverColor = null, Color? pressColor = null, Color? idleColor = null)
        {
            var idle = idleColor ?? Colors.Transparent;
            var hover = hoverColor ?? Color.FromArgb("#2A2C31");
            var press = pressColor ?? Color.FromArgb("#34363C");

            var pointer = new PointerGestureRecognizer();
            pointer.PointerEntered += (_, _) => SetBackground(element, hover);
            pointer.PointerExited += (_, _) => SetBackground(element, idle);
            pointer.PointerPressed += (_, _) => SetBackground(element, press);
            pointer.PointerReleased += (_, _) => SetBackground(element, hover);
            element.GestureRecognizers.Add(pointer);
        }

        // ★ RETOUCHE (01/09, direction "Hybride Claude") : changement de couleur
        // instantané remplacé par un fondu court (120ms) — principe emprunté à
        // VS Code/Zed (transitions rares mais réelles, jamais de rebond) plutôt
        // qu'un simple SetBackground(). MAUI n'a PAS de "BackgroundColorTo" natif
        // (contrairement à FadeTo/ScaleTo) — implémenté ici à la main via
        // VisualElement.Animate (le mécanisme bas niveau sur lequel FadeTo/ScaleTo
        // eux-mêmes reposent), en interpolant chaque canal R/G/B/A.
        private static void SetBackground(View element, Color color)
        {
            if (element is VisualElement ve)
                AnimateBackgroundColor(ve, color);
            else
                element.BackgroundColor = color;
        }

        /// <summary>
        /// Exposée publique : réutilisée par CustomMenuBarView.xaml.cs (boutons
        /// Min/Max/Fermer, patron d'attache séparé — voir le commentaire de cette
        /// classe) pour ne pas dupliquer une 2e fois la même interpolation R/G/B/A.
        /// </summary>
        public static void AnimateBackgroundColor(VisualElement element, Color target, uint length = 120)
        {
            var start = element.BackgroundColor ?? Colors.Transparent;
            element.Animate(
                name: "HoverEffects.BackgroundColor",
                callback: t => element.BackgroundColor = new Color(
                    (float)(start.Red + (target.Red - start.Red) * t),
                    (float)(start.Green + (target.Green - start.Green) * t),
                    (float)(start.Blue + (target.Blue - start.Blue) * t),
                    (float)(start.Alpha + (target.Alpha - start.Alpha) * t)),
                length: length,
                easing: Easing.Linear);
        }
    }
}
