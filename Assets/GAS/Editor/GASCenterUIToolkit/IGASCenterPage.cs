using UnityEngine.UIElements;

namespace GAS.Editor
{
    internal interface IGASCenterPage
    {
        string Id { get; }
        string Title { get; }
        string Description { get; }

        VisualElement CreateView(GASCenterContext context);
        void Refresh();
    }
}
