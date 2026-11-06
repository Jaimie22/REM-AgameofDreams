using UnityEngine;

// Greybox helper: hides a finished model and puts a plain capsule in its place,
// with a small block on the front so you can see which way it's facing.
// The model stays in place underneath (just not drawn), so its animations and bones still work.
public static class GreyboxShape
{
    const string BodyName = "GreyboxBody";

    public static void Replace(GameObject root, Color colour)
    {
        if (root == null || root.transform.Find(BodyName) != null) return;

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            r.enabled = false;

        // Sized in world units, so it works whatever scale the model was imported at
        Vector3 s = root.transform.lossyScale;
        Vector3 inverse = new Vector3(
            s.x != 0f ? 1f / s.x : 1f,
            s.y != 0f ? 1f / s.y : 1f,
            s.z != 0f ? 1f / s.z : 1f);

        // Match the character's own collider if it has one, so what you see is what you bump into
        float height = 1.8f, radius = 0.4f;
        Vector3 centre = new Vector3(0f, 0.9f, 0f);

        CharacterController cc = root.GetComponent<CharacterController>();
        if (cc != null)
        {
            height = cc.height;
            radius = cc.radius;
            centre = cc.center;
        }

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = BodyName;
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = Vector3.Scale(centre, inverse);
        body.transform.localScale = Vector3.Scale(new Vector3(radius * 2f, height * 0.5f, radius * 2f), inverse);
        Paint(body, colour);

        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "GreyboxNose";
        nose.transform.SetParent(root.transform, false);
        nose.transform.localPosition = Vector3.Scale(
            new Vector3(centre.x, centre.y + height * 0.25f, centre.z + radius), inverse);
        nose.transform.localScale = Vector3.Scale(new Vector3(radius, radius * 0.8f, radius), inverse);
        Paint(nose, Color.Lerp(colour, Color.white, 0.5f));
    }

    static void Paint(GameObject obj, Color colour)
    {
        Collider c = obj.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);

        Renderer rend = obj.GetComponent<Renderer>();
        rend.material.SetColor("_BaseColor", colour);
        rend.material.SetColor("_Color", colour);
    }
}
