import AppKit
import PDFKit

let app = NSApplication.shared
app.setActivationPolicy(.accessory)

guard CommandLine.arguments.count >= 2 else {
    fputs("Usage: macos-print-pdf <pdf-path>\n", stderr)
    exit(2)
}

let pdfPath = CommandLine.arguments[1]
let url = URL(fileURLWithPath: pdfPath)

guard FileManager.default.fileExists(atPath: pdfPath),
      let document = PDFDocument(url: url) else {
    fputs("Could not load PDF.\n", stderr)
    exit(1)
}

guard let printOperation = document.printOperation(
    for: .shared,
    scalingMode: .pageScaleDownToFit,
    autoRotate: true
) else {
    fputs("Could not create print operation.\n", stderr)
    exit(1)
}

printOperation.showsPrintPanel = true
_ = printOperation.run()

// Exit successfully after the dialog closes, even when the user cancels.
exit(0)
