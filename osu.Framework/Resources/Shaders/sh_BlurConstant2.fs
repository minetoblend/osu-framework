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

lowp vec4 blur(mediump vec2 texCoord, mediump vec2 texSize)
{
	lowp vec4 sum = texture(sampler2D(m_Texture, m_Sampler), texCoord) * 0.150183;

	highp vec2 one_div_texsize = vec2(1.0) / texSize;
	
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(-3.5, -3.5) * one_div_texsize) * 0.003663;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(-3.5, -1.5) * one_div_texsize) * 0.014652;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(-3.5, 0.0) * one_div_texsize) * 0.025641;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(-3.5, 1.5) * one_div_texsize) * 0.014652;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(-3.5, 3.5) * one_div_texsize) * 0.003663;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(-1.5, -3.5) * one_div_texsize) * 0.014652;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(-1.5, -1.5) * one_div_texsize) * 0.058608;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(-1.5, 0.0) * one_div_texsize) * 0.095238;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(-1.5, 1.5) * one_div_texsize) * 0.058608;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(-1.5, 3.5) * one_div_texsize) * 0.014652;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(0.0, -3.5) * one_div_texsize) * 0.025641;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(0.0, -1.5) * one_div_texsize) * 0.095238;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(0.0, 1.5) * one_div_texsize) * 0.095238;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(0.0, 3.5) * one_div_texsize) * 0.025641;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(1.5 -3.5) * one_div_texsize) * 0.014652;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(1.5 -1.5) * one_div_texsize) * 0.058608;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(1.5 0.0) * one_div_texsize) * 0.095238;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(1.5 1.5) * one_div_texsize) * 0.058608;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(1.5 3.5) * one_div_texsize) * 0.014652;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(3.5 -3.5) * one_div_texsize) * 0.003663;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(3.5 -1.5) * one_div_texsize) * 0.014652;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(3.5 0.0) * one_div_texsize) * 0.025641;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(3.5 1.5) * one_div_texsize) * 0.014652;
	sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + vec2(3.5 3.5) * one_div_texsize) * 0.003663;

	// this is supposed to be unnecessary with https://github.com/ppy/veldrid-spirv/pull/4,
	// but blurring is still broken on some Apple devices when removing it (at least on an M2 iPad Pro and an iPhone 12).
	// todo: investigate this.
	float one = g_BackbufferDraw ? 0 : 1;

	return sum * one;
}

void main(void)
{
	o_Colour = blur(v_TexCoord, g_TexSize);
}

#endif