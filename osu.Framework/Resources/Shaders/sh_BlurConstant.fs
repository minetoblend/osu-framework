#ifndef BLUR_FS
#define BLUR_FS

#include "sh_Utils.h"

#undef INV_SQRT_2PI
#define INV_SQRT_2PI 0.39894

layout(location = 2) in mediump vec2 v_TexCoord;

layout(std140, set = 0, binding = 0) uniform m_BlurParameters
{
	mediump vec2 g_TexSize;
	int g_Radius;
	mediump float g_Sigma;
	highp vec2 g_BlurDirection;
};

layout(set = 1, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler;

layout(location = 0) out vec4 o_Colour;

lowp vec4 blur(highp vec2 direction, mediump vec2 texCoord, mediump vec2 texSize)
{
	lowp vec4 sum = texture(sampler2D(m_Texture, m_Sampler), texCoord) * 0.236514;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + g_BlurDirection * 1.5 / texSize ) * 0.220455;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord - g_BlurDirection * 1.5 / texSize ) * 0.220455;	
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + g_BlurDirection * 3.5 / texSize) * 0.161288;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord - g_BlurDirection * 3.5 / texSize) * 0.161288;

	// this is supposed to be unnecessary with https://github.com/ppy/veldrid-spirv/pull/4,
	// but blurring is still broken on some Apple devices when removing it (at least on an M2 iPad Pro and an iPhone 12).
	// todo: investigate this.
	float one = g_BackbufferDraw ? 0 : 1;

	return sum * one;
}

void main(void)
{
	o_Colour = blur(g_BlurDirection, v_TexCoord, g_TexSize);
}

#endif