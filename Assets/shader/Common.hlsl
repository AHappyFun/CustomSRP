
#ifndef CUSTOM_COMMON_INCLUDE

#define CUSTOM_COMMON_INCLUDE

float Square(float v){
	return v * v;
}

float DistanceSquared(float3 pA, float pB){
	return dot(pA - pB, pA - pB);
}

#endif
