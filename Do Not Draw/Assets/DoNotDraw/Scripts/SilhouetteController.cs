using System.Collections;
using UnityEngine;

/// <summary>
/// 오른쪽으로 기울며 등장한 뒤, 카메라 시야에 포착되면 잠시 대기했다가
/// 왼쪽으로 빠르게 이동하며 사라지는 실루엣 연출용 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class SilhouetteController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("시야 판정에 사용할 카메라. 비워두면 Camera.main을 사용합니다.")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Renderer targetRenderer;

    [Header("등장 연출 (우측 기울임)")]
    [SerializeField] private float appearDuration = 0.6f;
    [SerializeField] private float tiltAngle = 15f;

    [Header("발각 후 퇴장 연출 (좌측 이동)")]
    [Tooltip("카메라 시야에 잡힌 뒤 퇴장을 시작하기까지 대기하는 시간입니다.")]
    [SerializeField] private float spottedDelay = 0.5f;
    [SerializeField] private float retreatDistance = 3f;
    [SerializeField] private float retreatDuration = 0.2f;

    private Coroutine sequenceRoutine;
    private Quaternion uprightRotation;

    private void Awake()
    {
        if (null == targetRenderer)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (null == targetCamera)
        {
            targetCamera = Camera.main;
        }

        uprightRotation = transform.rotation;
    }

    /// <summary>
    /// 실루엣을 오른쪽으로 기울이며 등장시킵니다. 카메라 시야에 포착되면
    /// 잠시 대기한 뒤 왼쪽으로 빠르게 이동하며 자동으로 사라집니다.
    /// </summary>
    public void Appear()
    {
        if (null != sequenceRoutine)
        {
            StopCoroutine(sequenceRoutine);
        }

        transform.rotation = uprightRotation;
        gameObject.SetActive(true);
        sequenceRoutine = StartCoroutine(AppearAndWatchRoutine());
    }
    void Start()
    {
        Appear();       
    }
    private IEnumerator AppearAndWatchRoutine()
    {
        // 우측으로 기울여지며 나타남
        Quaternion tiltedRotation = uprightRotation * Quaternion.Euler(0f, 0f, -tiltAngle);
        yield return StartCoroutine(RotateRoutine(uprightRotation, tiltedRotation, appearDuration));

        // 카메라 시야 범위 안에 잡힐 때까지 대기
        while (!IsVisibleToCamera())
        {
            yield return null;
        }

        yield return new WaitForSeconds(spottedDelay);

        // 좌측으로 빠르게 이동하며 사라짐
        yield return StartCoroutine(RetreatRoutine());

        gameObject.SetActive(false);
        sequenceRoutine = null;
    }

    private IEnumerator RotateRoutine(Quaternion from, Quaternion to, float duration)
    {
        if (duration <= 0f)
        {
            transform.rotation = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.rotation = Quaternion.Slerp(from, to, ratio);
            yield return null;
        }

        transform.rotation = to;
    }

    private IEnumerator RetreatRoutine()
    {
        Vector3 leftDirection = null != targetCamera ? -targetCamera.transform.right : Vector3.left;
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + leftDirection.normalized * retreatDistance;

        if (retreatDuration <= 0f)
        {
            transform.position = endPosition;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < retreatDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / retreatDuration);
            transform.position = Vector3.Lerp(startPosition, endPosition, ratio);
            yield return null;
        }

        transform.position = endPosition;
    }

    private bool IsVisibleToCamera()
    {
        if (null == targetCamera || null == targetRenderer)
        {
            return false;
        }

        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(targetCamera);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, targetRenderer.bounds);
    }
}
