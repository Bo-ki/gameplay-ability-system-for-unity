namespace GAS.Runtime
{
    public enum PeriodResetPolicy
    {
        NeverRefresh, //不重置Effect的周期计时

        ResetOnSuccessfulApplication //每次apply成功后重置Effect的周期计时
    }
}
