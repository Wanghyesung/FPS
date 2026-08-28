using UnityEngine;

/*///////////////////////////////////////////
                HitEffectView
목적 : 풀에서 재사용되는 히트 이펙트용. SetActive(true)만으로는 ParticleSystem이
       다시 재생되지 않으므로(Awake는 최초 1회뿐), OnEnable에서 매번 Play한다.
 *///////////////////////////////////////////

[RequireComponent(typeof(ParticleSystem))]
public sealed class HitEffectView : MonoBehaviour
{
    private ParticleSystem m_refParticles;

    private void Awake()
    {
        m_refParticles = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        m_refParticles.Play();
    }
}
