
#version 330 core

uniform sampler2D uTexture;
uniform mat4 uModel;

uniform vec3 uLightDirection;
uniform vec3 uLightAmbientColor;
uniform vec3 uLightDiffuseColor;
uniform vec3 uLightSpecularColor;

uniform vec3 uCameraPosition;
uniform float uShininess;

in vec2 uv;
in vec3 fragNormal;
in vec3 fragPosition;

out vec4 outColor;

void main()
{
    // Correct normal transformation
    mat3 normalMatrix =
        transpose(inverse(mat3(uModel)));

    vec3 normal = normalize(
        normalMatrix * fragNormal
    );

    // Material color
    vec4 texColor = texture(uTexture, uv);
    vec3 surfaceColor = texColor.rgb;

    // Directional lighting
    vec3 lightDirection =
        normalize(uLightDirection);

    float brightness = max(
        dot(normal, lightDirection),
        0.0
    );

    // View direction
    vec3 viewDirection = normalize(
        uCameraPosition - fragPosition
    );

    // Blinn-Phong specular lighting
    vec3 halfwayDirection = normalize(
        lightDirection + viewDirection
    );

    float specularIntensity = pow(
        max(dot(normal, halfwayDirection), 0.0),
        uShininess
    ) * brightness;

    // Ambient lighting
    vec3 ambient =
        surfaceColor * uLightAmbientColor;

    // Diffuse lighting
    vec3 diffuse =
        surfaceColor *
        brightness *
        uLightDiffuseColor;

    // Specular lighting
    vec3 specular =
        specularIntensity *
        uLightSpecularColor;

    vec3 finalColor =
        ambient + diffuse + specular;

    outColor = vec4(
        finalColor,
        texColor.a
    );
}
