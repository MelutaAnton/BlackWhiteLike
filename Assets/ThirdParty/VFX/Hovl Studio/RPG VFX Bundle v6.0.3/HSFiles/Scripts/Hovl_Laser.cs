using UnityEngine;

namespace Hovl
{
    public class Hovl_Laser : MonoBehaviour
    {
        public int damageOverTime = 30;

        public GameObject HitEffect;
        public float HitOffset = 0;
        public bool useLaserRotation = false;

        public float MaxLength;
        public float laserSize = 1f;

        private LineRenderer Laser;

        public float MainTextureLength = 1f;
        public float NoiseTextureLength = 1f;

        private Vector4 Length = new Vector4(1, 1, 1, 1);
        private bool LaserSaver = false;
        private bool UpdateSaver = false;

        private ParticleSystem[] Effects;
        private ParticleSystem[] Hit;

        private Vector3 startScale;
        private float startWidthMultiplier;

        void Start()
        {
            Laser = GetComponent<LineRenderer>();
            Effects = GetComponentsInChildren<ParticleSystem>();
            Hit = HitEffect.GetComponentsInChildren<ParticleSystem>();

            startScale = transform.localScale;

            if (Laser != null)
                startWidthMultiplier = Laser.widthMultiplier;

            ApplyLaserSize();
        }

        void Update()
        {
            ApplyLaserSize();

            float safeSize = Mathf.Max(0.0001f, laserSize);

            Laser.material.SetTextureScale("_MainTex", new Vector2(Length[0] / safeSize, Length[1]));
            Laser.material.SetTextureScale("_Noise", new Vector2(Length[2] / safeSize, Length[3]));

            if (Laser != null && UpdateSaver == false)
            {
                Laser.SetPosition(0, transform.position);

                RaycastHit hit;

                if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, MaxLength))
                {
                    Laser.SetPosition(1, hit.point);

                    HitEffect.transform.position = hit.point + hit.normal * HitOffset;

                    if (useLaserRotation)
                        HitEffect.transform.rotation = transform.rotation;
                    else
                        HitEffect.transform.LookAt(hit.point + hit.normal);

                    foreach (var AllPs in Effects)
                    {
                        if (!AllPs.isPlaying) AllPs.Play();
                    }

                    float distance = Vector3.Distance(transform.position, hit.point);

                    Length[0] = MainTextureLength * distance;
                    Length[2] = NoiseTextureLength * distance;
                }
                else
                {
                    var EndPos = transform.position + transform.forward * MaxLength;

                    Laser.SetPosition(1, EndPos);
                    HitEffect.transform.position = EndPos;

                    foreach (var AllPs in Hit)
                    {
                        if (AllPs.isPlaying) AllPs.Stop();
                    }

                    float distance = Vector3.Distance(transform.position, EndPos);

                    Length[0] = MainTextureLength * distance;
                    Length[2] = NoiseTextureLength * distance;
                }

                if (Laser.enabled == false && LaserSaver == false)
                {
                    LaserSaver = true;
                    Laser.enabled = true;
                }
            }
        }

        private void ApplyLaserSize()
        {
            float safeSize = Mathf.Max(0.0001f, laserSize);

            transform.localScale = startScale * safeSize;

            if (Laser != null)
                Laser.widthMultiplier = startWidthMultiplier * safeSize;
        }

        public void DisablePrepare()
        {
            if (Laser != null)
            {
                Laser.enabled = false;
            }

            UpdateSaver = true;

            if (Effects != null)
            {
                foreach (var AllPs in Effects)
                {
                    if (AllPs.isPlaying) AllPs.Stop();
                }
            }
        }
    }
}