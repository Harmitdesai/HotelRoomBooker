using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;

namespace ConsoleApp1
{
    //delegate declaration for creating events
    public delegate void PriceCutEvent(double roomPrice, Thread agentThread);
    public delegate void OrderProcessEvent(Order order, double orderAmount);
    public delegate void OrderCreationEvent();
    public class MainClass
    {
        public static MultiCellBuffer buffer;
        public static Thread[] travelAgentThreads;
        public static bool hotelThreadRunning = true;
        public static void Main(string[] args)
        {
            Console.WriteLine("Inside Main");
            buffer = new MultiCellBuffer();
            Hotel hotel = new Hotel();
            TravelAgent travelAgent = new TravelAgent();
            Thread hotelThread = new Thread(new ThreadStart(hotel.hotelFun));
            hotelThread.Name = "Hotel";
            hotelThread.Start();
            Hotel.PriceCut += new PriceCutEvent(travelAgent.agentOrder);
            Console.WriteLine("Price cut event has been subscribed");
            TravelAgent.orderCreation += new OrderCreationEvent(hotel.takeOrder);
            Console.WriteLine("Order creation event has been subscribed");
            OrderProcessing.OrderProcess += new OrderProcessEvent(travelAgent.orderProcessConfirm);
            Console.WriteLine("Order process event has been subscribed");
            travelAgentThreads = new Thread[5];
            for (int i = 0; i < 5; i++)
            {
                Console.WriteLine("Creating travel agent thread {0}", (i + 1));
                travelAgentThreads[i] = new Thread(travelAgent.agentFun);
                travelAgentThreads[i].Name = (i + 1).ToString();
                travelAgentThreads[i].Start();
            }
        }
    }
    public class MultiCellBuffer
    {
        // Each cell can contain an order object
        private const int bufferSize = 3; //buffer size
        int usedCells;
        private Order[] multiCells; // ? mark make the type nullable: allow to assign null value
        public static Semaphore getSemaph;
        public static Semaphore setSemaph;

        //lock objects for thread safety
        private static readonly object _lockObjSet = new object();
        private static readonly object _lockObjGet = new object();

        public MultiCellBuffer() //constructor
        {
            this.usedCells = 0;
            this.multiCells = new Order[bufferSize];

            // Initialize semaphores
            // Semaphore for getting an order
            getSemaph = new Semaphore(0, bufferSize);
            // Semaphore for setting an order
            setSemaph = new Semaphore(bufferSize, bufferSize);
        }
        public void SetOneCell(Order data)
        {
          
            // Wait for a free cell
            // and lock the buffer for writing
            // to prevent other threads from writing
            // until this thread is done writing
            setSemaph.WaitOne();
            lock (_lockObjSet)
            {
                for (int i = 0; i < bufferSize; i++)
                {
                    if (multiCells[i] == null)
                    {
                        Console.WriteLine("Setting in buffer cell");
                        multiCells[i] = data;
                        usedCells++;
                        Console.WriteLine("Exit setting in buffer");
                        break;
                    }
                }
            }
            getSemaph.Release();
        }
        public Order GetOneCell()
        {
           
            // Wait for an order to be available
            // and lock the buffer for reading
            // to prevent other threads from reading
            // until this thread is done reading
            getSemaph.WaitOne();
            lock (_lockObjGet)
            {
                for (int i = 0; i < bufferSize; i++)
                {
                    if (multiCells[i] != null)
                    {
                        Order order = multiCells[i];
                        multiCells[i] = null;
                        usedCells--;
                        Console.WriteLine("Exit reading buffer");
                        setSemaph.Release();
                        return order;
                    }
                }
            }
            setSemaph.Release();
            return null; // This line should never be reached
        }
    }
    public class Order
    {
        //identity of sender of order
        private string senderId;
        //credit card number
        private long cardNo;
        //unit price of room from hotel
        private double unitPrice;
        //quantity of rooms to order
        private int quantity;
        //parametrized constructor
        public Order(string senderId, long cardNo, double unitPrice, int quantity)
        {
           
            // Initialize the order object with the provided parameters
            this.senderId = senderId;
            this.cardNo = cardNo;
            this.unitPrice = unitPrice;
            this.quantity = quantity;
        }
        //getter methods
        public string getSenderId()
        {
            return this.senderId;
        }
        public long getCardNo()
        {
            return this.cardNo;
        }
        public double getUnitPrice()
        {
            return this.unitPrice;
        }
        public int getQuantity()
        {
            return this.quantity;
        }
    }
    public class OrderProcessing
    {
        public static event OrderProcessEvent OrderProcess;
        //method to check for valid credit card number input
        public static bool creditCardCheck(long creditCardNumber)
        {
            // Check if the credit card number is valid
            // For simplicity, let's assume a valid credit card number is between 1000 and 9999
            if (creditCardNumber < 1000 || creditCardNumber > 9999)
            {
                Console.WriteLine("Invalid credit card number");
                return false;
            }
            return true;
        }
        //method to calculate the final charge after adding taxes, location, charges, etc
        public static double calculateCharge(double unitPrice, int quantity)
        {
            // Calculate the total charge based on unit price and quantity
            return unitPrice * quantity;
        }
        //method to process the order
        public static void ProcessOrder(Order order)
        {
            // Process the order and charge the credit card
            Thread.Sleep(200);
            creditCardCheck(order.getCardNo());
            double orderAmount = calculateCharge(order.getUnitPrice(), order.getQuantity());
            // Invoke the event to notify the travel agent
            OrderProcess?.Invoke(order, orderAmount);
        }
    }
    public class TravelAgent
    {
        public static event OrderCreationEvent orderCreation;
        public double roomPrice = 100;
        public double travelAgentId = 0;


        public void agentFun()
        {
            // Main thread for travel agent
            // it will stop when hotel thread stops
            Console.WriteLine("Starting Travel agent now");
            travelAgentId = Convert.ToDouble(Thread.CurrentThread.Name);
            while (MainClass.hotelThreadRunning)
            {
                Thread.Sleep(400);
            }
        }
        public void orderProcessConfirm(Order order, double orderAmount)
        {
            // Confirm the order and charges
            Console.WriteLine($"Travel Agent {order.getSenderId()}'s order is confirmed. The amount to be charged is ${orderAmount}");
        }
        private void createOrder(string senderId, double unitPrice)
        {
            Console.WriteLine($"Inside create order");
            // random credit card number and quantity
            Random rnd = new Random();
            long cardNo = rnd.Next(1000, 9999);
            int quantity = rnd.Next(30, 70);
            // create order object
            Order order = new Order(senderId, cardNo, unitPrice, quantity);
            Console.WriteLine("Setting in buffer cell");
            // set the order in the buffer
            MainClass.buffer.SetOneCell(order);
            // invoke the order creation event
            orderCreation?.Invoke();
        }
        public void agentOrder(double roomPrice, Thread travelAgent) // Callback from hotel thread
        {
            Thread.Sleep(200);
            // updating room price
            this.roomPrice = roomPrice;
            // create order
            Console.WriteLine($"Incoming order for room with price {roomPrice}");
            createOrder(Thread.CurrentThread.Name, roomPrice);
        }
    }
    public class Hotel
    {
        static double currentRoomPrice = 100; //random current agent price
        static int threadNo = 0;
        static int eventCount = 0;
        public static event PriceCutEvent PriceCut;
        public void hotelFun()
        {
            while (eventCount < 10)
            {
                //sleep for 1 second
                Thread.Sleep(600);
                //update the price
                updatePrice(pricingModel());
            }
            MainClass.hotelThreadRunning = false;
        }
        //using random method to generate random room prices
        public double pricingModel()
        {
            Thread.Sleep(200);
            // Generate a random price between 70 and 150
            Random rnd = new Random();
            double price = rnd.Next(70, 150);
            return price;
        }
        public void updatePrice(double newRoomPrice)
        {
            double oldPrice = currentRoomPrice;
            currentRoomPrice = newRoomPrice;

            Console.WriteLine($"New price is {currentRoomPrice}");
            // Check if the new price is less than the current price
            // If so, update the current price and invoke the event
            if (oldPrice < currentRoomPrice)
            {
                currentRoomPrice = newRoomPrice;
                PriceCut?.Invoke(currentRoomPrice, Thread.CurrentThread);
                eventCount++;
                Console.WriteLine("Updating the price and calling ");
            }
        }
        public void takeOrder() // callback from travel agent
        {
            Order order = MainClass.buffer.GetOneCell();
            if (order != null)
            {
                //Process the order
                OrderProcessing.ProcessOrder(order);
                Thread.Sleep(200);
            }
            else
            {
                Console.WriteLine("No order received");
            }
        }
    }
}
