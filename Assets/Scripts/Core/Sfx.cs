using System;
using UnityEngine;

namespace ConcertDefense.Core
{
    public enum SfxId
    {
        Shoot,
        Impact,
        Build,
        Teleport,
        Perfect,
        Good,
        BossArrive,
        Feedback,
        Ultimate,
        StageHit
    }

    /// <summary>
    /// Reproductor central de efectos de sonido (GDD 9). Vive en [GameManagers].
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public SfxId id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        [Tooltip("Clips asignados a cada efecto.")]
        [SerializeField] private Entry[] entries = new Entry[0];

        [Tooltip("Fuente 2D usada para los efectos (se crea sola si falta).")]
        [SerializeField] private AudioSource source;

        [Tooltip("Tiempo mínimo entre dos reproducciones del mismo efecto, para no saturar.")]
        [SerializeField] private float minInterval = 0.06f;

        private static Sfx instance;
        private float[] lastPlayTime;

        private void Awake()
        {
            instance = this;

            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }

            lastPlayTime = new float[Enum.GetValues(typeof(SfxId)).Length];
            for (int i = 0; i < lastPlayTime.Length; i++) lastPlayTime[i] = -10f;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Play(SfxId id)
        {
            if (instance != null) instance.PlayInternal(id);
        }

        private void PlayInternal(SfxId id)
        {
            int index = (int)id;
            if (Time.unscaledTime - lastPlayTime[index] < minInterval) return;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].id != id || entries[i].clip == null) continue;

                lastPlayTime[index] = Time.unscaledTime;
                source.PlayOneShot(entries[i].clip, entries[i].volume <= 0f ? 1f : entries[i].volume);
                return;
            }
        }
    }
}
