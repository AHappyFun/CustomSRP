#ifndef CUSTOM_UNITY_INPUT_INCLUDE
#define CUSTOM_UNITY_INPUT_INCLUDE

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;   //每次物体绘制的时候设置，每个物体单独一个矩阵
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;  //real不是有效类型，取决于平台的float或half

	//lightmap缩放
	float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;

	//球谐参数 SH9需要的7个float4
	//0和1阶
	float4 unity_SHAr; 
	float4 unity_SHAg;
	float4 unity_SHAb;
	//2阶
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;

	//LPPV参数
	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;
CBUFFER_END

CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;  //每个摄像机绘制的时候有一个，同一摄像机共用
CBUFFER_END

 float4x4 unity_MatrixV;
 float4x4 glstate_matrix_projection;

 float3 _WorldSpaceCameraPos;



#endif 