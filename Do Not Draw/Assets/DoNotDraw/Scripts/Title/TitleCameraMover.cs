using UnityEngine;

public class TitleCameraMover : MonoBehaviour
{
    [Header("회전 속도 설정")]
    [Tooltip("회전 주기 속도 (낮을수록 천천히 움직입니다)")]
    [SerializeField] private float rotateSpeed = 0.15f;

    [Header("회전 각도 제한 (Euler Angles)")]
    [Tooltip("상하 고개 끄덕임 각도 제한 (Pitch)")]
    [SerializeField] private float maxPitch = 8.0f;

    [Tooltip("좌우 둘러보기 각도 제한 (Yaw)")]
    [SerializeField] private float maxYaw = 15.0f;

    [Tooltip("좌우 기울임 각도 제한 (Roll, 0으로 두면 화면이 기울지 않음)")]
    [SerializeField] private float maxRoll = 1.5f;

    [Header("스무딩 강도")]
    [Tooltip("회전 보간 속도")]
    [SerializeField] private float smoothFactor = 2.0f;

    // 시작 시 기준 회전값
    private Quaternion initialRotation;

    // 축별 독립된 노이즈를 위한 시드 오프셋
    private Vector3 noiseOffset;

    private void Start()
    {
        // 씬 시작 시 카메라가 보고 있는 기본 각도를 기준으로 저장
        initialRotation = transform.rotation;

        // 매번 다른 무작위 패턴으로 시작하도록 초기화
        noiseOffset = new Vector3(
            Random.value * 100f, 
            Random.value * 100f, 
            Random.value * 100f
        );
    }

    private void Update()
    {
        float time = Time.time * rotateSpeed;

        // PerlinNoise 값(0.0 ~ 1.0)을 (-1.0 ~ 1.0)으로 매핑하여 각 축의 오프셋 계산
        float pitch = (Mathf.PerlinNoise(time + noiseOffset.x, 0f) - 0.5f) * 2f * maxPitch;
        float yaw   = (Mathf.PerlinNoise(0f, time + noiseOffset.y) - 0.5f) * 2f * maxYaw;
        float roll  = (Mathf.PerlinNoise(time + noiseOffset.z, time) - 0.5f) * 2f * maxRoll;

        // 초기 각도 기준 상대 회전값 생성
        Quaternion targetRotation = initialRotation * Quaternion.Euler(pitch, yaw, roll);

        // 부드럽게 Slerp 회전
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothFactor);
    }
}