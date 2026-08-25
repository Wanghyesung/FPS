/*///////////////////////////////////////////
                IPoolable
목적 : 오브젝트 풀에서 대여/반납될 때 호출되는 생명주기 콜백 계약.
       Instantiate/Destroy 대신 SetActive 재사용을 하므로, 생성자/OnDestroy 대신
       이 두 콜백에서 인스턴스 상태를 초기화·정리한다.
 *///////////////////////////////////////////

public interface IPoolable
{
    void OnSpawned();
    void OnDespawned();
}
