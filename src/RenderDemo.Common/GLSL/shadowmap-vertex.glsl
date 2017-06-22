﻿#version 330 core

uniform mat4 ProjectionMatrixBuffer;

uniform mat4 ViewMatrixBuffer;

uniform mat4 WorldMatrixBuffer;

in vec3 in_position;

void main()
{
    vec4 worldPos = WorldMatrixBuffer * vec4(in_position, 1);
    vec4 viewPos = ViewMatrixBuffer * worldPos;
    vec4 screenPos = ProjectionMatrixBuffer * viewPos;
    gl_Position = screenPos;
    // Normalize depth range
    gl_Position.z = gl_Position.z * 2.0 - gl_Position.w;
}