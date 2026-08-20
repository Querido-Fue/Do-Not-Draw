using UnityEngine;

namespace DoNotDraw.Audio
{
    // 배경/앰비언트 루프 사운드가 VolumeManager.bgmVolume을 따라가게 하기 위한 배율.
    // 루프 재생 중인 AudioSource.volume에 이 배율만 곱해서 쓰면 됩니다.
    // 예: ambientSource.volume = baseVolume * BgmVolume.Scale;
    public static class BgmVolume
    {
        public static float Scale => VolumeManager.Instance != null ? VolumeManager.Instance.bgmVolume : 1f;
    }
}
