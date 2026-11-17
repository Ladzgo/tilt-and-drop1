// Native helpers for iOS: haptics (UIFeedbackGenerator) and the share sheet.
#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController();

extern "C" {

void TD_Haptic(int style)
{
    // 0 light, 1 medium, 2 heavy, 3 success, 4 failure (matches TiltDrop.Game.Haptic)
    if (style <= 2) {
        UIImpactFeedbackStyle s = style == 0 ? UIImpactFeedbackStyleLight : (style == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy);
        UIImpactFeedbackGenerator* g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        [g prepare];
        [g impactOccurred];
    } else {
        UINotificationFeedbackGenerator* g = [[UINotificationFeedbackGenerator alloc] init];
        [g prepare];
        [g notificationOccurred:(style == 3 ? UINotificationFeedbackTypeSuccess : UINotificationFeedbackTypeError)];
    }
}

void TD_Share(const char* text)
{
    NSString* s = [NSString stringWithUTF8String:text];
    UIActivityViewController* vc = [[UIActivityViewController alloc] initWithActivityItems:@[s] applicationActivities:nil];
    UIViewController* root = UnityGetGLViewController();
    vc.popoverPresentationController.sourceView = root.view;
    vc.popoverPresentationController.sourceRect = CGRectMake(root.view.bounds.size.width / 2, root.view.bounds.size.height / 2, 1, 1);
    [root presentViewController:vc animated:YES completion:nil];
}

}
