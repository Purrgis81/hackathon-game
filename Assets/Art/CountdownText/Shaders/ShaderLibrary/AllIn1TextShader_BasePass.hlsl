#ifndef ALLIN1TEXT_BASE_PASS_INCLUDED
#define ALLIN1TEXT_BASE_PASS_INCLUDED

// Per-draw time and preset, sent by MatchTextOverlay. Ordinary countdown text uses zero.
float4 _ResultAnimation;

void AnimateResultGlyph(float seed, out float scale, out float rise, out float alpha)
{
    // The baked result mesh stores a 1-based left-to-right glyph index as its seed.
    float index = round(seed) - 1;
    float time = _ResultAnimation.x;
    if (_ResultAnimation.y < 1.5)
    {
        // LevelBurst: center outward, .05 s apart, with an elastic settle and a looping wave.
        float age = max(0, time - abs(index - 3.0) * .05);
        float t = saturate(age * 1.4);
        scale = t == 0 ? 0 : t == 1 ? 1 : 1 + exp2(-10 * t) * sin((10 * t - .75) * 2.0943951);
        rise = -.48 * pow(1 - saturate(age * 2.4), 3);
        rise += .075 * sin(time * 8.8 - index * .79) * smoothstep(.45, .86, age);
        alpha = saturate(age * 6);
    }
    else
    {
        // CloudDrift: soft left-to-right emergence, followed by a smaller CloudBob.
        float age = max(0, time - index * .12);
        float t = saturate(age * .9);
        float eased = 1 - pow(1 - t, 3);
        scale = lerp(.7, 1, eased);
        rise = -.31 * (1 - eased) + .027 * sin(time * 5.03 - index * .70) * eased;
        alpha = smoothstep(0, 1, t) * .90;
    }
}

FragmentData AllIn1TextShaderVertex(VertexData v)
{
	FragmentData o;
	
	INITIALIZE_FRAGMENT_DATA(o)
	
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	//TMP flags fake bold (no bold typeface in the font's weight table) by negating the scale it packs: in uv2.y up to TextMeshPro 3.0, in uv0.w
	//since uGUI 2.0, whose uv2 holds the face texture mapping (read there, its bottom vertices looked bold). Real bold glyphs come from another font asset
#if UNITY_VERSION >= 202320
	float bold = step(v.texcoord0.w, 0);
#else
	float bold = step(v.texcoord1.y, 0);
#endif
	float faceDilate = _CustomFaceDilate + bold * 0.2;

	//The per quad and per text data the toolkit wrote into the mesh (QuadData)
	QuadData quad = DecodeQuadData(v.texcoord1.x, v.texcoord2.x, v.texcoord2.y);
	g_RectSize = float4(v.texcoord2.zw, 0.0, 0.0);
	g_EmSize = v.tangent.w;
	g_RectAxisX = v.tangent.xyz;
	g_RectAxisY = v.normal;
	float2 positionRS = v.texcoord3.xy;
	float3 characterCenter = float3(v.texcoord3.w, 0.0, 0.0);

	/* Expanding mesh */
	float2 charUVnormalized = quad.cornerUV;

	//Margin per axis in rect space, so narrow glyphs get the same horizontal room as wide ones.
	//Pushed along the glyph's own axes so rotated glyphs keep their shape, and through the rect's axes so the text's rotation and scale carry over
	float2 quadSizeRS = max(UnpackHalf2(v.texcoord3.z), 0.00001);
	float2 quadSizeTexels = float2(quad.uvCorners.y - quad.uvCorners.x, quad.uvCorners.w - quad.uvCorners.z) * float2(_TextureWidth, _TextureHeight);
	//1 glyph, 2 underline or strikethrough slice, 3 mark slice, plus 3 for Extrude's back copies. Line slices sit edge to edge along their run,
	//so they only grow across it; marks keep TMP's box; a back copy grows like its source
	float quadKind = quad.kind;
	float shapeKind = GetBaseQuadKind(quadKind);
	float2 kindMask = shapeKind > 2.5 ? float2(0.0, 0.0) : (shapeKind > 1.5 ? float2(0.0, 1.0) : float2(1.0, 1.0));
	bool drawsFace;
	bool drawsBehind;
	GetQuadLayers(quadKind, drawsFace, drawsBehind);
	float2 meshMargin = CalculateMeshMargin(quadSizeRS, quadSizeTexels, quad.characterAspectRatio, faceDilate, drawsFace, drawsBehind);
	meshMargin *= kindMask;
	float2 cornerSign = sign(charUVnormalized - 0.5);
	float2 glyphRight = quad.glyphRight;
	float2 glyphUp = float2(-glyphRight.y, glyphRight.x);

	float2 cornerMargin = meshMargin;
#if defined(_EXTRUDE_ON)
	if (drawsBehind)
	{
		cornerMargin += max(0.0, cornerSign * CalculateExtrudeMargin(glyphRight, quadSizeRS, quadSizeTexels)) * kindMask;
	}
#endif

	float2 marginOffsetRS = glyphRight * (cornerSign.x * cornerMargin.x) + glyphUp * (cornerSign.y * cornerMargin.y);
	v.vertex.xyz += RectToMeshDirection(marginOffsetRS);
    if (_ResultAnimation.y > .5)
    {
        float scale, rise, alpha;
        AnimateResultGlyph(quad.timingSeed, scale, rise, alpha);
        float2 fromCenter = (glyphRight * cornerSign.x * quadSizeRS.x + glyphUp * cornerSign.y * quadSizeRS.y) * .5;
        v.vertex.xyz += RectToMeshDirection((fromCenter + marginOffsetRS) * (scale - 1) + float2(0, rise));
        v.color.a *= alpha;
    }

	//Corners sit at 0 or 1, so this moves each one out by its own margin and keeps the atlas mapping for one sided growth
	charUVnormalized += cornerSign * cornerMargin / quadSizeRS;
	SET_CHAR_UV_NORMALIZED(o, charUVnormalized);

	SET_UV_NORMALIZED_RAW(o, quad.cornerUV);

	float timingSeed = quad.timingSeed;
	float3 shaderTime = GET_SHADER_TIME(o) + timingSeed;
	v.vertex = ApplyVertexEffects(v.vertex, shaderTime, timingSeed, characterCenter, charUVnormalized, glyphRight, quadSizeRS);

	//Where the grown corner sits in the rect, 0 to 1. Vertex effects move the letters, not what they show
	float2 rectUV = (positionRS + marginOffsetRS) / max(g_RectSize.xy, 0.00001);

	float4 clipPos = OBJECT_TO_CLIP_SPACE(v);
#if !defined(_EXTRUDE_ON)
	//Back copies only draw Extrude, so a layer without it collapses them to a point and rasterizes nothing
	clipPos = quadKind > 3.5 ? float4(0.0, 0.0, 0.0, 1.0) : clipPos;
#endif

	float3 viewDirWS = normalize(GetViewDirWS(v.vertex.xyz)); 
	
	float bias = CalculateMainBias(faceDilate);
	
	float4 faceColor = v.color * _FaceColor;
	faceColor.rgb *= faceColor.a;

	o.pos = clipPos;

	float2 faceUVRaw = v.texcoord0.xy;
	
	float2 faceUV = faceUVRaw;

	float pixelWorldSize = (unity_OrthoParams.y * 2.0) / _ScreenParams.y;
	float pixelSizeMask = smoothstep(0.05, 0.2, pixelWorldSize);
	
	
	SET_RECT_UV(o, rectUV)

	SET_FACE_COLOR(o, faceColor)

	SET_FACE_UV(o, faceUV)
	
	SET_BIAS(o, bias)

	SET_FACE_DILATE(o, faceDilate)

	SET_VIEWDIR_WS(o, viewDirWS)

	SET_POSITION_OS(o, v.vertex.xyz)
	
	SET_QUAD_DATA(o, quad)

	SET_PIXEL_SIZE_MASK(o, pixelSizeMask)
	
	
	float2 texelSize = float2(1.0 / _TextureWidth, 1.0 / _TextureHeight);
	SET_TEXEL_SIZE(o, texelSize)
	
	float3 vertexVS = mul(UNITY_MATRIX_MV, v.vertex).xyz;
	SET_VERTEX_VS(o, vertexVS)
	
	return o;
}

float4 AllIn1TextShaderFragment(
	FragmentData i) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(i);

	GET_QUAD_DATA(i, quad)

	EffectsData data = CalculateEffectsData(i);

	float4 faceColor = GET_FACE_COLOR(i);
	float bias = GET_BIAS(i);
	//The text's colour alpha (fades, CanvasGroup) times the look's Face Color alpha, applied once at the end as TMP's shader does
	float vertexAlpha = faceColor.a;
	float3 positionOS = GET_POSITION_OS(i);

	
	float2 originalRectUV = GET_RECT_UV(i);
	
	float2 characterUVNormalized = GET_CHAR_UV_NORMALIZED(i);
	CREATE_CHARACTER_DATA_WITH_BIAS(mainCharacterData, characterUVNormalized, bias, quad)
	mainCharacterData.pixelSDF = data.pixelTexels / GetAtlasSpreadTexels();

	CREATE_CHARACTER_DATA(outline01Data, characterUVNormalized, quad)

	CREATE_CHARACTER_DATA(outline02Data, characterUVNormalized, quad)

	CREATE_CHARACTER_DATA(outline03Data, characterUVNormalized, quad)
	
	mainCharacterData = ApplyUVEffects_FragmentStage(mainCharacterData);
	
	//float2 charUVNormalized = data.characterUVNormalized;
	
	float2 rectUVDiff = mainCharacterData.rectUV - originalRectUV;
	mainCharacterData.uvNormalized += rectUVDiff;
	
	float4 uvCornersInAtlas = quad.uvCornersInAtlas;
	
	ConfigureCharacterData(
		data.characterUVCorners,
		uvCornersInAtlas,
		bias,
		data.texelSize,
		_CustomOutlineSoftness,
		mainCharacterData
	);
	
	mainCharacterData.uvNormalized += rectUVDiff;
	
	//The vertex stage premultiplied the face by the alpha, which comes back once at the end
	float4 objectColor = float4(faceColor.rgb / max(vertexAlpha, 0.0001), 1.0);
	
#if defined(OUTLINE_01_ENABLED)
	ConfigureOutline01(data.characterUVCorners, uvCornersInAtlas, data.texelSize, mainCharacterData, outline01Data);
#endif

#if defined(OUTLINE_02_ENABLED)
	ConfigureOutline02(data.characterUVCorners, uvCornersInAtlas, data.texelSize, mainCharacterData, outline02Data);
#endif

#if defined(OUTLINE_03_ENABLED)
	ConfigureOutline03(data.characterUVCorners, uvCornersInAtlas, data.texelSize, mainCharacterData, outline03Data);
#endif
	
	float4 col = objectColor;
	
	
	float characterBodyAlpha = mainCharacterData.characterMask * _InnerAlpha;
	col.rgb *= characterBodyAlpha;
	col.a = characterBodyAlpha;
	
	bool drawsFace;
	bool drawsBehind;
	GetQuadLayers(data.quadKind, drawsFace, drawsBehind);

	//A back copy starts from nothing: its glyph quad draws the text, after every back copy
	if (drawsFace)
	{
		col = ApplyColorEffects(col, data, mainCharacterData, outline01Data, outline02Data, outline03Data);
		col = ApplyLightingEffects(col, data, mainCharacterData, outline01Data, outline02Data, outline03Data);
		col = ApplyColorEffectsAfterLighting(col, data, mainCharacterData, outline01Data, outline02Data, outline03Data);
	}
	else
	{
		col = float4(0.0, 0.0, 0.0, 0.0);
	}

	if (drawsBehind)
	{
		col = ApplyExtrude(mainCharacterData, data, col);
		col = ApplyShadows(mainCharacterData, data, col);
	}
	col = ApplyAlphaEffects(col, mainCharacterData.rectUV, mainCharacterData);
	
	
	//col is premultiplied by its coverage
	float coverage = saturate(col.a);
	float3 finalRGB = col.rgb / max(coverage, 0.0001) * _ColorIntensity;
	float finalAlpha = coverage * vertexAlpha;

	//On the alpha, so every blending mode fades out at the mask's edge
#if defined(UNITY_UI_CLIP_RECT)
	finalAlpha *= UIRectClip(GET_POSITION_OS(i).xy);
#endif
	
	
	
#if defined(_BLENDINGMODE_MULTIPLY)
	col.rgb = lerp(1.0, finalRGB, finalAlpha);
#elif defined(_BLENDINGMODE_ADDITIVE)
	col.rgb = finalRGB * finalAlpha;
#else
	col.rgb = finalRGB * finalAlpha;
#endif

	col.a = finalAlpha;

#if defined(UNITY_UI_ALPHACLIP)
	clip(col.a - 0.001);
#endif
	
#if defined(_ALPHA_CUTOFF_ON)
	clip(col.a - _AlphaCutoffValue);
#endif
	
	return col;
}

#endif //ALLIN1TEXT_BASE_PASS_INCLUDED