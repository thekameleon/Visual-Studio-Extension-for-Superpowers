using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TheKameleon.Superpowers.InProcess;

internal sealed class SelectionIdentityProbe
{
    private readonly IVsMonitorSelection? selection;
    private readonly IVsSolution? solution;

    public SelectionIdentityProbe(IVsMonitorSelection? selection, IVsSolution? solution)
    {
        this.selection = selection;
        this.solution = solution;
    }

    public string Capture(string scope)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (selection is null || solution is null)
        {
            return "Unavailable: required selection/solution service is missing.";
        }

        var hierarchyPointer = IntPtr.Zero;
        var containerPointer = IntPtr.Zero;
        try
        {
            var hr = selection.GetCurrentSelection(out hierarchyPointer, out var itemId, out var multiple, out containerPointer);
            if (ErrorHandler.Failed(hr))
            {
                return $"Unavailable: selection acquisition failed (0x{hr:X8}).";
            }

            if (multiple is not null || itemId == VSConstants.VSITEMID_SELECTION)
            {
                return "Unavailable: multiple selection is not supported; select one node.";
            }

            if (itemId == VSConstants.VSITEMID_NIL)
            {
                return "Unavailable: no selected hierarchy item.";
            }

            var hierarchy = hierarchyPointer == IntPtr.Zero ? null : Marshal.GetObjectForIUnknown(hierarchyPointer) as IVsHierarchy;
            if (itemId == VSConstants.VSITEMID_ROOT && (hierarchyPointer == IntPtr.Zero || hierarchy is IVsSolution))
            {
                if (ErrorHandler.Failed(solution.GetSolutionInfo(out _, out var solutionPath, out _)))
                {
                    return "Unavailable: selected solution has no file identity.";
                }

                return new SelectionIdentity("Solution", Path.GetFileNameWithoutExtension(solutionPath), solutionPath).Describe(scope);
            }

            if (hierarchy is not IVsProject project ||
                ErrorHandler.Failed(solution.GetGuidOfProject(hierarchy, out var projectGuid)) || projectGuid == Guid.Empty)
            {
                return "Unavailable: selected hierarchy is not a loaded solution project.";
            }

            if (ErrorHandler.Failed(project.GetMkDocument(VSConstants.VSITEMID_ROOT, out var projectPath)) ||
                ErrorHandler.Failed(hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_Name, out var projectName)))
            {
                return "Unavailable: selected project has no name/path identity.";
            }

            if (itemId == VSConstants.VSITEMID_ROOT)
            {
                return new SelectionIdentity("Project", projectName as string, projectPath).Describe(scope);
            }

            if (ErrorHandler.Failed(hierarchy.GetGuidProperty(itemId, (int)__VSHPROPID.VSHPROPID_TypeGuid, out var itemType)) ||
                itemType != VSConstants.GUID_ItemType_PhysicalFile)
            {
                return "Unavailable: selected item is not a physical file (folders and virtual nodes are unsupported).";
            }

            if (ErrorHandler.Failed(project.GetMkDocument(itemId, out var filePath)) ||
                ErrorHandler.Failed(hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_Name, out var fileName)))
            {
                return "Unavailable: selected file has no name/path identity.";
            }

            return new SelectionIdentity("File", fileName as string, filePath, projectName as string, projectPath).Describe(scope);
        }
        finally
        {
            // GetCurrentSelection transfers ownership of these two IUnknown pointers, not the shared RCWs.
            if (containerPointer != IntPtr.Zero)
            {
                Marshal.Release(containerPointer);
            }
            if (hierarchyPointer != IntPtr.Zero)
            {
                Marshal.Release(hierarchyPointer);
            }
        }
    }
}
