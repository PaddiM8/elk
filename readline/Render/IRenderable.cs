namespace Elk.ReadLine.Render;

interface IRenderable
{
    bool IsActive { get; set; }

    void Render();
}