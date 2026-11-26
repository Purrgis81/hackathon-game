#ifndef ALLIN1TEXTSHADER_COMMON_STRUCTS
#define ALLIN1TEXTSHADER_COMMON_STRUCTS

struct AllIn1LightData
{
	float3 lightColor;
	float3 lightDir;
	float realtimeShadow;
	float4 shadowColor;
	float distanceAttenuation;
	uint layerMask;
};

struct ShadowCoordStruct
{
	float4 _ShadowCoord : TEXCOORD0;
	float4 pos : TEXCOORD1;
};

struct FogStruct
{
	float fogCoord : TEXCOORD0;
};

struct VertexData
{
	UNITY_VERTEX_INPUT_INSTANCE_ID

	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float4 color : COLOR;
	float4 texcoord0 : TEXCOORD0;
	float4 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
	float4 texcoord3 : TEXCOORD3;
};

//What each vertex carries for the effects, written by TextMaterialConfiguratorMaster.WriteVertexData. On the mesh, not the material, so texts
//sharing a material batch and nothing is written per frame. uGUI's batching moves the tangent and normal with the text, so they arrive as the rect's axes:
//	uv1.x	bits: corner (2), kind (3), extra padding (1), timing seed (8), glyph angle (10)
//	uv2		glyph rect x + width * 8192, y + height * 8192 (atlas texels), rect size
//	uv3		position from the rect's bottom left, quad width and height (two halves), glyph center x (rect space)
//	tangent	the rect's x axis, w the font size in rect units
//	normal	the rect's y axis
struct QuadData
{
	float2 cornerUV;			//0 or 1 per axis, bottom left to top right
	float kind;					//1 glyph, 2 underline or strikethrough slice, 3 mark slice, plus 3 for Extrude's back copies
	float timingSeed;
	float2 glyphRight;			//The glyph's right axis in rect space, unit length
	float4 uvCorners;			//The quad's atlas rect: the glyph rect plus TMP's padding
	float4 uvCornersInAtlas;	//The clamp rect: the glyph rect plus (atlas padding - 1)
	float characterAspectRatio;	//Glyph rect height over width
};

//Per text values that used to be material properties, set at the top of each stage from the mesh data
static float4 g_RectSize;		//xy: the text's rect size in rect units
static float g_EmSize;			//The font size in rect units, as TMP scales glyphs
static float3 g_RectAxisX;		//Vertex stage: the rect's axes in the space the mesh arrives in (the root canvas's for uGUI)
static float3 g_RectAxisY;

float3 RectToMeshDirection(float2 directionRS)
{
	return g_RectAxisX * directionRS.x + g_RectAxisY * directionRS.y;
}

#define INITIALIZE_FRAGMENT_DATA(input) \
	input.pos = 0; \
	input.interpolator_00 = 0; \
	input.interpolator_01 = 0; \
	input.interpolator_02 = 0; \
	input.interpolator_03 = 0; \
	input.interpolator_04 = 0; \
	input.interpolator_05 = 0; \
	input.interpolator_06 = 0; \
	input.interpolator_07 = 0; \
	input.interpolator_08 = 0; \
	input.interpolator_09 = 0; \
	input.interpolator_10 = 0;

struct FragmentData
{
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO

	float4 pos : SV_POSITION;
	float4 interpolator_00 : TEXCOORD0;
	float4 interpolator_01 : TEXCOORD1;
	float4 interpolator_02 : TEXCOORD2;
	float4 interpolator_03 : TEXCOORD3;
	float4 interpolator_04 : TEXCOORD4;
	float4 interpolator_05 : TEXCOORD5;
	float4 interpolator_06 : TEXCOORD6;
	//The quad data, the same on every corner of a quad
	nointerpolation float4 interpolator_07 : TEXCOORD7;
	nointerpolation float4 interpolator_08 : TEXCOORD8;
	nointerpolation float4 interpolator_09 : TEXCOORD9;
	nointerpolation float4 interpolator_10 : TEXCOORD10;
};

struct EffectsData
{
	float pixelSizeMask;
	
	float2 texelSize;

	float3 vertexOS;
	float3 vertexVS;
	float3 viewDirWS;
	
	float4 characterUVCorners;
	float4 uvCornersInAtlas;
	float4 faceColor;

	float2 glyphRight; //The glyph's right axis in rect space, unit length
	float quadKind; //characterCenter.w: 1 glyph, 2 line slice, 3 mark slice
	float pixelTexels; //A screen pixel in atlas texels, from the quad's UV before any UV effect snaps or bends it
};
	
struct CharacterData
{
	float2 uvNormalized; //0-1 from bottom-left quad to top-right quad
	float2 unclampedUV;
	float2 mainUV;
	float characterMask;
	float sdf;
	float sdfBlurred;
	float bias;
	float faceDilate; //Material dilate plus the bold weight, per glyph
	float timingSeed;
	float3 shaderTime;
	float characterAspectRatio;
	float2 rectUV;
	float rectAspectRatio;
	float pixelSDF; //A screen pixel in remapped SDF units, the narrowest edge ramp. 0 leaves edges hard
};


#define CREATE_CHARACTER_DATA_WITH_BIAS(param_variable_name, param_uv_normalized, param_bias, param_quad) \
	CharacterData param_variable_name; \
	param_variable_name.uvNormalized = param_uv_normalized; \
	param_variable_name.unclampedUV = 0; \
	param_variable_name.mainUV = 0; \
	param_variable_name.characterMask = 0; \
	param_variable_name.sdf = 0; \
	param_variable_name.sdfBlurred = 0; \
	param_variable_name.bias = param_bias; \
	param_variable_name.faceDilate = GET_FACE_DILATE(i); \
	param_variable_name.timingSeed = param_quad.timingSeed; \
	param_variable_name.shaderTime = GET_SHADER_TIME(i); \
	param_variable_name.characterAspectRatio = param_quad.characterAspectRatio; \
	param_variable_name.rectUV = GET_RECT_UV(i); \
	param_variable_name.rectAspectRatio = g_RectSize.y / max(g_RectSize.x, 0.00001); \
	param_variable_name.pixelSDF = 0;

#define CREATE_CHARACTER_DATA(param_variable_name, param_uv_normalized, param_quad) CREATE_CHARACTER_DATA_WITH_BIAS(param_variable_name, param_uv_normalized, 0, param_quad)


#endif //ALLIN1TEXTSHADER_COMMON_STRUCTS