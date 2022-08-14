#ifndef CUSTOM_UNITY_INPUT_INCLUDE
#define CUSTOM_UNITY_INPUT_INCLUDE

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;   //ÿ��������Ƶ�ʱ�����ã�ÿ�����嵥��һ������
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;  //real������Ч���ͣ�ȡ����ƽ̨��float��half

	//lightmap����
	float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;

	//��г���� SH9��Ҫ��7��float4
	//0��1��
	float4 unity_SHAr; 
	float4 unity_SHAg;
	float4 unity_SHAb;
	//2��
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;

	//LPPV����
	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;
CBUFFER_END

CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;  //ÿ����������Ƶ�ʱ����һ����ͬһ���������
CBUFFER_END

 float4x4 unity_MatrixV;
 float4x4 glstate_matrix_projection;

 float3 _WorldSpaceCameraPos;



#endif 