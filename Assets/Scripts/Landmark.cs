// #define FLIP // Comment out this line to flip the landmarks (internally) [technically need to flip here, but kept like this for backward compatibility].
// NOTE: image = cv2.flip(image, 1) in the Python side may also be of interest to you as well.

#if FLIP
public enum Landmark
{
    NOSE = 0,
    LEFT_EAR = 1,
    RIGHT_EAR = 2,
    LEFT_SHOULDER = 3,
    RIGHT_SHOULDER = 4,
    LEFT_ELBOW = 5,
    RIGHT_ELBOW = 6,
    LEFT_WRIST = 7,
    RIGHT_WRIST = 8,
    LEFT_PINKY = 9,
    RIGHT_PINKY = 10,
    LEFT_INDEX = 11,
    RIGHT_INDEX = 12,
    LEFT_HIP = 13,
    RIGHT_HIP = 14,
    LEFT_KNEE = 15,
    RIGHT_KNEE = 16,
    LEFT_ANKLE = 17,
    RIGHT_ANKLE = 18,
    LEFT_HEEL = 19,
    RIGHT_HEEL = 20,
    LEFT_FOOT_INDEX = 21,
    RIGHT_FOOT_INDEX = 22,
}
#else
public enum Landmark
{
    NOSE = 0,
    LEFT_EAR = 1,
    RIGHT_EAR = 2,
    LEFT_SHOULDER = 3,
    RIGHT_SHOULDER = 4,
    LEFT_ELBOW = 5,
    RIGHT_ELBOW = 6,
    LEFT_WRIST = 7,
    RIGHT_WRIST = 8,
    LEFT_PINKY = 9,
    RIGHT_PINKY = 10,
    LEFT_INDEX = 11,
    RIGHT_INDEX = 12,
    LEFT_HIP = 13,
    RIGHT_HIP = 14,
    LEFT_KNEE = 15,
    RIGHT_KNEE = 16,
    LEFT_ANKLE = 17,
    RIGHT_ANKLE = 18,
    LEFT_HEEL = 19,
    RIGHT_HEEL = 20,
    LEFT_FOOT_INDEX = 21,
    RIGHT_FOOT_INDEX = 22,
}
#endif