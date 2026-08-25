using System.Text;
using TMPro;
using UnityEngine;

/*///////////////////////////////////////////
                HUDView
목적 : 체력/무기·탄약 텍스트를 그리기만 하는 Passive View. 로직이 전혀 없고
       HUDSystem가 호출하는 Refresh 계열 표시 메서드만 노출한다.
       문자열 조합은 캐싱된 StringBuilder로 처리해 갱신 시 할당을 최소화한다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class HUDView : MonoBehaviour
{
    [SerializeField] private TMP_Text m_refHealthText;
    [SerializeField] private TMP_Text m_refAmmoText;
    [SerializeField] private TMP_Text m_refZoneText;
    [SerializeField] private TMP_Text m_refItemText;

    private readonly StringBuilder m_sbBuffer = new StringBuilder(64);

    public void RefreshHealth(int _iCurrent, int _iMax)
    {
        if (m_refHealthText == null)
        {
            return;
        }

        m_sbBuffer.Clear();
        m_sbBuffer.Append("HP ").Append(_iCurrent).Append(" / ").Append(_iMax);
        m_refHealthText.SetText(m_sbBuffer);
    }

    public void RefreshAmmo(string _strWeaponName, int _iMagazine, int _iReserve)
    {
        if (m_refAmmoText == null)
        {
            return;
        }

        m_sbBuffer.Clear();
        m_sbBuffer.Append(_strWeaponName).Append("  ").Append(_iMagazine).Append(" / ").Append(_iReserve);
        m_refAmmoText.SetText(m_sbBuffer);
    }

    /// <summary>자기장 페이즈/남은 시간/경계까지의 거리(안전권이면 음수 이하로 들어온다).</summary>
    public void RefreshZone(int _iPhase, int _iRemainingSeconds, int _iDistanceOutside)
    {
        if (m_refZoneText == null)
        {
            return;
        }

        m_sbBuffer.Clear();
        m_sbBuffer.Append("ZONE ").Append(_iPhase).Append("   ").Append(_iRemainingSeconds).Append('s');

        if (_iDistanceOutside > 0)
        {
            m_sbBuffer.Append("   OUT ").Append(_iDistanceOutside).Append('m');
        }
        else
        {
            m_sbBuffer.Append("   SAFE");
        }

        m_refZoneText.SetText(m_sbBuffer);
    }

    public void RefreshInventory(int _iBandageCount, int _iMedkitCount, bool _bHasVest)
    {
        if (m_refItemText == null)
        {
            return;
        }

        m_sbBuffer.Clear();
        m_sbBuffer.Append("BANDAGE ").Append(_iBandageCount)
            .Append("   MEDKIT ").Append(_iMedkitCount)
            .Append("   VEST ").Append(_bHasVest ? "ON" : "-");
        m_refItemText.SetText(m_sbBuffer);
    }
}
