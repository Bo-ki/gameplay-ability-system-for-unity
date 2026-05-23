namespace GAS.Runtime
{
    public enum DurationRefreshPolicy
    {
        NeverRefresh, //不刷新Effect的持续时间

        RefreshOnSuccessfulApplication //每次apply成功后刷新Effect的持续时间, denyOverflowApplication如果为True则多余的Apply不会刷新Duration
    }
}
