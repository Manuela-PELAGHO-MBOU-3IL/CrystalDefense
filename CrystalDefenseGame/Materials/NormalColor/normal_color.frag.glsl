#version 330 core

in vec3 worldNormal;
out vec4 outColor;

void main()
{
    // The interpolated normal is normalized again; -1..1 is mapped to the color range 0..1.
    outColor = vec4(normalize(worldNormal) * 0.5 + 0.5, 1.0);
}
