import sys, os
sys.path.append("devscons/v1.0")
import dev

env = dev.Dev(ENV = os.environ, tools = ['msvs','nant','envconfig','nsis','solution','csc','msdevenv'], toolpath=["devscons/v1.0"])
env.DevSetBuild()
Export("env")
SConscript('src/SConscript')
#Create packages
env.DevPackage()
env.DevInstaller(name='FeedGenerators')
