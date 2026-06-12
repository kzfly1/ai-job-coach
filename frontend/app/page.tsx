import {Button} from "@/components/ui/button";
import {Card, CardContent, CardHeader, CardTitle} from "@/components/ui/card";

export default function Home() {
    return (
        <main className=" flex min-h-screen items-center justify-center p-8">
            <Card className="w-full max-w-md">
                <CardHeader>
                    <CardTitle>AI Job Coach</CardTitle>
                </CardHeader>

                <CardContent>
                    <p className="mb-4 text-sm text-muted-foreground">
                        Frontend scaffold is ready.
                    </p>

                    <Button>Test Button</Button>
                </CardContent>
            </Card>
        </main>
    );
}