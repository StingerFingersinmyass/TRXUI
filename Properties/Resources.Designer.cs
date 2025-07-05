using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace TRXLoader.Properties
{
	// Token: 0x02000004 RID: 4
	[global::System.CodeDom.Compiler.GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
	[global::System.Diagnostics.DebuggerNonUserCode]
	[global::System.Runtime.CompilerServices.CompilerGenerated]
	internal class Resources
	{
		// Token: 0x06000012 RID: 18 RVA: 0x00002585 File Offset: 0x00000785
		internal Resources()
		{
		}

		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000013 RID: 19 RVA: 0x0000258D File Offset: 0x0000078D
		[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Advanced)]
		internal static global::System.Resources.ResourceManager ResourceManager
		{
			get
			{
				if (global::TRXLoader.Properties.Resources.resourceMan == null)
				{
					global::TRXLoader.Properties.Resources.resourceMan = new global::System.Resources.ResourceManager("TRXLoader.Properties.Resources", typeof(global::TRXLoader.Properties.Resources).Assembly);
				}
				return global::TRXLoader.Properties.Resources.resourceMan;
			}
		}

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000014 RID: 20 RVA: 0x000025B9 File Offset: 0x000007B9
		// (set) Token: 0x06000015 RID: 21 RVA: 0x000025C0 File Offset: 0x000007C0
		[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Advanced)]
		internal static global::System.Globalization.CultureInfo Culture
		{
			get
			{
				return global::TRXLoader.Properties.Resources.resourceCulture;
			}
			set
			{
				global::TRXLoader.Properties.Resources.resourceCulture = value;
			}
		}

		// Token: 0x04000009 RID: 9
		private static global::System.Resources.ResourceManager resourceMan;

		// Token: 0x0400000A RID: 10
		private static global::System.Globalization.CultureInfo resourceCulture;
	}
}
