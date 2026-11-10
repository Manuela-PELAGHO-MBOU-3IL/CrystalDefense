using Engine;
using OpenTK.Mathematics;

namespace CrystalDefense.PlayerAndProjectiles;

// Finds the point in the arena that is under the mouse cursor.
//
// Idea: the screen is a window into the 3D scene. The mouse position defines a ray that starts at the
// camera and goes "through" the cursor into the scene. Where this ray crosses a horizontal plane
// (for example at the height of the projectiles) is the point the player aims at.
public static class MouseAiming
{
    public static bool TryGetPointOnPlane(
        Vector2 mousePosition, Vector2i windowSize, Camera camera, float planeHeight, out Vector3 point)
    {
        point = Vector3.Zero;
        if (windowSize.X <= 0 || windowSize.Y <= 0)
            return false;

        // 1. Pixel position → normalized device coordinates (-1 … 1, y upwards).
        float x = 2.0f * mousePosition.X / windowSize.X - 1.0f;
        float y = 1.0f - 2.0f * mousePosition.Y / windowSize.Y;

        // 2. The inverse of view × projection takes a point from the screen back into the 3D world.
        float aspectRatio = (float)windowSize.X / windowSize.Y;
        Matrix4 viewProjection = camera.ViewMatrix * camera.GetProjectionMatrix(aspectRatio);
        Matrix4 inverse = viewProjection.Inverted();

        // 3. Two points of the ray: on the near plane (z = -1) and on the far plane (z = +1) of the camera.
        Vector3 near = Unproject(new Vector4(x, y, -1.0f, 1.0f), inverse);
        Vector3 far = Unproject(new Vector4(x, y, 1.0f, 1.0f), inverse);
        Vector3 direction = far - near;

        // 4. Intersection of the ray with the horizontal plane y = planeHeight.
        if (MathF.Abs(direction.Y) < 0.000001f)
            return false;   // the ray is parallel to the plane

        float t = (planeHeight - near.Y) / direction.Y;
        if (t < 0.0f)
            return false;   // the plane is behind the camera

        point = near + direction * t;
        return true;
    }

    // Homogeneous coordinates: divide by w to get the real 3D point back.
    private static Vector3 Unproject(Vector4 screenPoint, Matrix4 inverse)
    {
        Vector4 world = screenPoint * inverse;
        return world.Xyz / world.W;
    }
}
