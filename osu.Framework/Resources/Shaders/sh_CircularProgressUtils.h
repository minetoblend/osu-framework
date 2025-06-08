#ifndef CIRCULAR_PROGRESS_UTILS_H
#define CIRCULAR_PROGRESS_UTILS_H

#undef TWO_PI
#define TWO_PI 6.28318530718

highp float distanceToRing(in vec2 p, in vec2 n, in float r, float th)
{
    p.x = abs(p.x);
    p = mat2x2(n.x, n.y, -n.y, n.x) * p;
    return max(abs(length(p) - r) - th * 0.5, length(vec2(p.x, max(0.0, abs(r - p.y) - th * 0.5))) * sign(p.x));
}

lowp float progressAlphaAt(highp vec2 pixelPos, mediump float progress, mediump float thickness, mediump float cornerRadius, highp float texelSize)
{
    // This is a bit of a hack to make progress appear smooth if it's radius < texelSize by making it more transparent while leaving thickness the same
    lowp float subAAMultiplier = 1.0;
    subAAMultiplier = clamp(thickness / (texelSize * 2.0), 0.1, 1.0);
    
    thickness = max(thickness, texelSize * 2.0);
    cornerRadius *= thickness * 0.5;

    mediump float outerRadius = 1.0 - cornerRadius;
    mediump float innerRadius = 1.0 - thickness + cornerRadius;
    
    highp float angle = progress * TWO_PI;

    highp vec2 cs = vec2(cos(angle * 0.5), sin(angle * 0.5));

    pixelPos = (pixelPos - 0.5) * vec2(2, -2);
    pixelPos = mat2x2(cs.x, cs.y, -cs.y, cs.x) * pixelPos;
    
    float distance = distanceToRing(pixelPos, cs, (outerRadius + innerRadius) * 0.5 - texelSize, outerRadius - innerRadius) - cornerRadius + texelSize;

    return smoothstep(texelSize, 0.0, distance * 0.5) * subAAMultiplier;
}

#endif
