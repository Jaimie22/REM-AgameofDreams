using System.Collections;
using UnityEngine;

// A level that plays a short animation when you enter it, before you get control.
// HideForIntro is called as soon as the level is chosen, so nothing shows before it's built.
// The player is already standing at the start, frozen, and the animation can take over the camera.
// tint is this level's crystal colour, previousTint is the level before it. The crystals
// start in the old colour and settle into the new one, which links the levels together.
public interface IFaceIntro
{
    void HideForIntro();
    IEnumerator PlayIntro(DreamCamera camera, Color tint, Color previousTint);
}
