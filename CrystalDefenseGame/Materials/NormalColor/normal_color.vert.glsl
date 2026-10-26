#version 330 core

layout(location = 0) in vec3 localPosition;
layout(location = 1) in vec3 localNormal;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 worldNormal;

void main()
{
    // w = 0: the normal is a direction, the translation of the model matrix is ignored
    worldNormal = (vec4(localNormal, 0.0) * uModel).xyz;
    gl_Position = vec4(localPosition, 1.0) * uModel * uView * uProjection;
}
