public class MainRunner {
    public static class MyThing extends TestLibrary.ClientIThing01 {
        private static TestLibrary.IThing01 stashedThing;

        @Override
        public int someMethod(int x) {
            return x * 333;
        }

        @Override
        public void stashThing(TestLibrary.IThing01 thing2) {
            stashedThing = thing2;
        }

        @Override
        public void testThrow(boolean doThrow) throws TestLibrary.InterfaceMethodException {
            if (doThrow) {
                System.out.println("Java: throwing in testThrow()");
                throw new TestLibrary.InterfaceMethodException(6543);
            }
        }

        @Override
        public void onTimerTick() {
            System.out.println("!!!! Java: onTimerTick()");
        }

        @Override
        public TestLibrary.IThing01 getStashedThing() {
            TestLibrary.IThing01 ret = stashedThing;
            stashedThing = null;
            return ret;
        }
    }

    public static void main(String[] args) {
        // dump module constants
        System.out.println("== module constants ==");
        System.out.printf("  - global01: %d\n", TestLibrary.global01);
        System.out.printf("  - anotherGlobal: %d\n", TestLibrary.anotherGlobal);
        System.out.printf("  - lastIntGlobal: %d\n", TestLibrary.lastIntGlobal);

        int result = TestLibrary.sum(101, 202, 303);
        System.out.printf("result: %d\n", result);

        for (int i = 0; i< 10; i++) {
            int x = i;
            int result2 = TestLibrary.execFunc01(a -> a * x);
            System.out.printf("execFunc01(a -> a * 3) = %d\n", result2);
        }
        TestLibrary.IntFunc woot = TestLibrary.returnStashedFunc();
        System.out.printf("server pre-stashed func (1234) = %d\n", woot.apply(1234));

        TestLibrary.IntFunc myFunc = a -> a * 31;
        TestLibrary.stashFunc(myFunc);
        TestLibrary.stashFunc(myFunc);
        TestLibrary.stashFunc(myFunc);
        TestLibrary.stashFunc(myFunc);

        TestLibrary.releaseFunc();

        try (TestLibrary.IThing01 serverThing = TestLibrary.makeThing()) {

            for (int i=0; i< 100; i++) {
                System.out.printf("someMethod(%d) = %d\n", i, serverThing.someMethod(i));
            }

            TestLibrary.operateOnThing(serverThing, 1031);

            // mything test
            TestLibrary.IThing01 clientThing = new MyThing();
            TestLibrary.operateOnThing(clientThing, 111);

            // crashes here ...
            serverThing.stashThing(clientThing);
            TestLibrary.IThing01 testing = serverThing.getStashedThing();

            // server object will be released here
        } catch (Exception e) {
            e.printStackTrace();
        }

        TestLibrary.IThing01 myThing2 = new MyThing();
        TestLibrary.saveThing(myThing2);
        TestLibrary.IThing01 maybeMyThing2 = TestLibrary.getSavedThing();
        assert myThing2 == maybeMyThing2;
        TestLibrary.clientStashTest(myThing2);

        try {
            System.out.println("$$$$$ Java testing negative: 1");
            TestLibrary.testNegative(1);
            System.out.println("$$$$$ Java testing negative: -1");
            TestLibrary.testNegative(-1);
        } catch (TestLibrary.NegativeException e) {
            System.out.printf("Caught Negative Exception! %d\n", e.value);
        }

        TestLibrary.testClientThrow(myThing2);

        TestLibrary.startTimer(myThing2, 1);
        try {
            Thread.sleep(5000);
        } catch (InterruptedException e) {
            e.printStackTrace();
        }
        TestLibrary.stopTimer();

        TestLibrary.shutdown();
    }
}
